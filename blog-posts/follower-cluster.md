---
title: Follower clusters
---
# Follower clusters in Kusto (Azure Data Explorer)

*Last modified: 01/07/2021*

In Kusto, one can [attach a database](https://docs.microsoft.com/en-us/azure/data-explorer/follower){:target="_blank"} located in a one cluster to another cluster.
- Databases that are followed are attached in read-only mode, allowing access to data that was ingested on the leader cluster.
- A cluster can follow multiple databases from several other clusters.
- A cluster can have multiple other clusters following its databases.
- It is possible to override certain policies on the follower, in order to have better control over security and cost.

* TOC
{:toc}

## How is a database followed?

- The leader and follower rely on Azure blob storage accounts for this synchronization.
  - The storage is owned by the leader, and the follower is given read-only access to the set of artifacts that are necessary for the operation to be successful.
  - The limitation mandating both the follower and leader to be in the same region stems from this, in order to avoid cross-region latency and extra charges when reading storage artifacts.
  - Compressed storage artifacts that are within the follower's effective caching period are automatically fetched to the local disks of the follower's nodes, without having to re-ingest the data from the leader or from its source.
- A follower cluster periodically synchronizes changes from its leader(s). As this is done periodically, there can be a lag of a few seconds to a few minutes on the follower.
  - The length of the lag depends on the amount of metadata changes that were made in between sequential synchronizations.

## Benefits of a follower cluster

### Workload isolation

As the follower is a completely different resource, and its compute is isolated from its leader's - Any requests running on a leader do not affect the resources utilization on the follower, and vice versa.

This allows having different workloads run across different resources, without one impacting the performance of the other, but without having to ingest the same data more than once.

**Examples**

- **Dashboards & alerts vs. long term analytics:** A leader cluster can serve frequent queries that are used by mission-critical tools and applications that look at the recent 'head' of the data (e.g. the last 24 hours), while a follower cluster can have a longer caching period (e.g. the last 90 days) for the same data set, and run heavier computations over larger volumes of data. Each cluster may have a different size, that fits its business requirements.
- **Testing:** A follower cluster in a staging environment can run performance or A/B tests against the same data that is available in the Production environment, without affecting the leader.
- **Cost reduction:** A team of analysts runs ad-hoc queries occasionally against a data set, during work hours. They can spin up their own follower cluster, sized according to their compute & caching requirements. Once it's no longer required, it can be suspended or deleted.
- **Cost management:** Two or more organizations can have a separate bill for their resources. Each resource can be sized according to each organization's needs.

### Data sharing

Providing partners and customers with access to your data, or a specific subset of it, is possible using [Azure data share](https://docs.microsoft.com/en-us/azure/data-explorer/data-share){:target="_blank"}. The data is ingested once, and there's no need to export or import it. The underlying implementation uses the mechanics described in this post.

<p align="center">
  <img title="Follower clusters in Kusto (Azure Data Explorer)" src="https://docs.microsoft.com/en-us/azure/data-explorer/media/data-share/adx-datashare-image.png">
</p>

## Policy overrides

While the database is followed in read-only mode, it is possible for the follower to override certain properties of its local copy of the database - in order to have better control over security and cost.

### Selecting specific tables, external tables & materialized views

It is possible for a database attached as a follower to define that only a subset of the tables, external tables and/or materialized views will be available on the follower.

This is useful for controlling who has access to which dataset on which resource, as well as controlling the costs.

The definition can be done using lists of tables to exclude and lists of tables to include. Wildcards (`*`) are supported as well.

**Examples**

Database `DB1` on cluster `C1` has the following 6 tables: `tableA`, `tableA2`, `tableA_private`, `tableB`, `tableC`, `tableD`.

1. Database `DB1` is followed on cluster `C2`, with tables to include: `["tableA", "tableB", "tableC"]`
   - Only tables `tableA`, `tableB`, `tableC` are available in `DB1` on `C2`.
2. Database `DB1` is followed on cluster `C3`, with tables to exclude: `["tableD", tableE"]`
   - All tables except for `tableD` are available in `DB1` on `C3`.
3. Database `DB1` is followed on cluster `C4`, with tables to include: `["tableA*", "tableB*"]`, and tables to exclude: `["tableAPrivate"]`
   - Only tables `tableA`, `tableA2`, `tableB` are available in `DB1` on `C4`.

### Authorized principals

It is possible for a database attached as a follower to define a set of authorized principals different than that defined on the leader. This override is only possible at the database-level.

This is useful for controlling who has access to which dataset on which resource.

- The follower can:
    - Have the original set of principals, as defined on the leader (override kind is `none`).
    - Add additional principals on top of those defined on the leader (override kind is `union`).
    - Completely replace the set of principals defined on the leader (override kind is `replace`).

The control commands for managing this setup can be found [here](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/management/cluster-follower#add-follower-database-principals){:target="_blank"}.

### Caching policy

It is possible for a database attached as a follower to define a set of caching policies different than that defined on the leader. This override can be defined at the database-level, and/or at the table-level.

This is useful for controlling the cost, and making sure that only data that is frequently accessed is cached.

- The follower can:
    - Have the original set of policies, as defined on the leader (override kind is `none`).
    - Add additional policies on top of those defined on the leader (override kind is `union`).
    - Completely replace the set of policies on the leader (override kind is `replace`).

The control commands for managing this setup can be found [here](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/management/cluster-follower#alter-follower-database-policy-caching){:target="_blank"} for database-level, and [here](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/management/cluster-follower#alter-follower-table-policy-caching){:target="_blank"} for table-level.

## Advanced settings

### Auto-prefetch

By default, when the follower cluster periodically synchronizes the changes from its leader(s), it only fetches metadata objects and makes new data queryable, without immediately fetching the underlying data artifacts.
- This allows for the periodic synchronization to be very quick, and make the latest data available for queries on the follower very shortly after it was ingested on the leader (usually up to a few seconds later).
- The underlying data artifacts are periodically *warmed* from the leader's blob storage accounts to the follower's nodes' local disks, unless they were required for queries that ran before then.
  - This means that the first queries running against the follower that target the most recently-ingested data may have some delays.

It is possible to configure the database on the follower with `auto-prefetch` set to `true`.
- By doing so, the periodic synchronizes will force the *warming* of the underlying data artifacts, and will finish only once those are on the follower's nodes' local disks.
- As a result -
  - Data that is querable on the follower is expected to be cached and queries running against it will not incur the aforementioned delay.
  - It is possible that the freshness of the data on the follower will be somewhat degraded, as the periodic synchronization may take longer to complete.
    - Therefore, it is recommended to use this setting **only when necessary**, and to measure its impact against the workload's performance requirements.

The control command for managing this setup can be found [here](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/management/cluster-follower#alter-follower-database-prefetch-extents){:target="_blank"}.

Another *advanced technique* would be to define a [stored function](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/query/schema-entities/stored-functions){:target="_blank"} in the database, that unions the table on the leader and on the follower, such that the latest data is taken from the leader and the rest - from the follower. This function should be run on the follower.
- This is only needed if you must have the data latency on the follower to match that on the leader. In all other cases, you can skip this technique.
- For example:
    
    ```
    .create function MyTable(_starttime:datetime, _endtime:datetime)
    {
        let _t = min_of(ago(5min), _endtime);
        union
        (
            // Data from up to 5 minutes ago is queried from the current (follower) cluster
            MyTable
            | where Timestamp between(_starttime .. _t)        
        ),
        (
            // Data from the last 5 minutes is queried from the other (leader) cluster
            cluster('leader.westus.kusto.windows.net').database('MyDatabase').table('MyTable')
            | where _endtime > _t
            | where current_cluster_endpoint() != 'leader.westus.kusto.windows.net' 
            | where Timestamp between(_t .. _endtime)
        )
    }
    ```

**Note:** It is generally recommended that workloads that require access to the most recently ingested data will run against the leader, whereas workloads that do not have that requirement, or can withstand some delay for this specific case, will run on a follower.

---

{% include  share.html %}