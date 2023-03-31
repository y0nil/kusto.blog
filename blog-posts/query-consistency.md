---
title: Query consistency
---
# Follower clusters in Kusto

*Last modified: 03/31/2023*

By default, queries in Kusto run with *strong* [consistency](https://learn.microsoft.com/en-us/azure/data-explorer/kusto/concepts/queryconsistency){:target="_blank"}.
Meaning - the query planning stage and the query finalization stage occur on the node that is in charge of
managing updates in the database. This node is called the *databaes admin node*, and by default there's a single database admin node in the cluster.
The same node is in charge of executing all control commands that occur in the context of databases is manages, and committing changes done by them to the database metadata.

While *strong* consistency is advantageous in providing queries access to the most recent updates (data appends, data deletion, schema modifications, etc.) that occurred in the database,
under very high loads this could overload the database admin node, and impact its availability.

Using *weak* consistency enables spreading that load to additional nodes in the cluster that can serve as *query heads*.
In this mode queries don't have that guarantee - clients making queries might observe some latency (up to a few minutes) between updates made in the database, and their queries
reflecting those changes.

For example, if 1000 records are ingested each minute into a table in the database, queries over that table running with *strong* consistency will have access to the most-recently ingested
records, whereas queries over that table running with *weak* consistency may not have access to a few thousands of recrods from the last few minutes.

* TOC
{:toc}

## Weakly consistent query heads

By default - 20% of the nodes in the cluster, with a minimum of 2 nodes, and a maximum of 30 nodes - can serve as weakly consistent query heads.
These parameters, as well as a few others, can be controlled using the cluster-level [Query weak consistency policy](https://learn.microsoft.com/en-us/azure/data-explorer/kusto/management/query-weak-consistency-policy){:target="_blank"}.

The same policy allows controlling the refresh rate of the database metadata on the weakly consistency query heads. By default, these nodes will start fetching the
latest database metadata every 2 minutes.

It's advised to start with the default values, and only adjust if necessary.

### Fetching the database metadata

The database metadata, as mentioned earlier, is managed by the *database admin node*. Each transaction that modifies it (e.g., appends data, drops data, modifies the schema, etc.)
gets committed to this node's memory, and changes are also written to persistent storage.

When a weakly consistency query head starts refreshing the database metadata, it pulls the delta of changes between the current it currently holds in memory, and the changes that
are in the current version of the database. This delta is downloaded from persistent storage.

In order to not increase the load on persistent storage, the amount of nodes in the cluster that can serve as weakly consistency query heads is limited, and so is the frequency at which
they fetch the latest version of the metadata.

## Weak consistency modes

There are 3 modes of *weak* query consistency:

|Mode                      |Description                                                                                                                         |
|--------------------------|------------------------------------------------------------------------------------------------------------------------------------|
|Random                    | Queries are routed randomly to one of the nodes in the cluster that can serve as a weakly consisteny query head.                   |
|Affinitized by database   | All queries that run in the context of the same database get routed to the same weakly consisteny query head.                      |
|Affinitized by query text | All queries that have the same hash for their query text get routed to the same weakly consisteny query head.                      |
|Affinitized by session ID | All queries that have the same hash for their session ID (provided separately) get routed to the same weakly consisteny query head.|

## When should I use weak consistency?

Whenever you want to reduce the load from the database admin node, and don't have a strong dependency on updates that occurred in the database in the last few minutes.

For example, if you are running the following query, which aggregates the daily number of error records in the last week, your insights will probably not be impacted if
records from the past few minutes are ommitted.

```
my_table
| where timestamp between(ago(7d) .. now())
| where level == "error"
| summarize count() by level, startofday(Timestamp)
```

### When should I use affinity by query?

This mode of weak consistency can be helpful when you're also leveraging the [Query results cache](https://learn.microsoft.com/en-us/azure/data-explorer/kusto/query/query-results-cache){:target="_blank"}. That way, repeating weakly consistent queries that are run frequently by the same identity can leverage results cached from recent executions
of the same query, and reduce the load on the cluster.

### When should I use affinity by database?

This mode of weak consistency can be helpful if it is important for you that queries running against the same database will all get executed against the *same* (though, not most recent) version of the database metadata.

If, however, there's imbalance in the amount of queries running against databases in the cluster (e.g. 70% of queries are run in the context of a specific database), then the query
head serving queries for that database will be more loaded than other query heads in the cluster, which is suboptimal.

### When should I use affinity by session ID?

This mode of of weak consistency can be helpful if it is important for you that queries that belong to the same user activity/session will all get executed against the *same* (though, not most recent) version of the database metadata. 

It does, however, require you to explicitly specify the session ID as part of each query's client request properties. See [below](#specifying-in-client-request-properties).

## When shouldn't I use weak consistency?

1. When you have have a strong dependency on updates that occurred in the database in the last few minutes.

  For example, if you are running the following query, which counts the number of error records in the 5 minutes, and triggers an alert if that count is larger than 0.

  ```
  my_table
  | where timestamp between(ago(5m)..now())
  | where level == "error"
  | count
  ```
2. When the database metadata is very large (e.g. there are millions of data shards/extents in the database) - this could result with weakly consistent query heads spending
   resources on frequently downloading large metadata artifacts from persistent storage, and potentially increase the odds of transient failures in those downloads.

## How do I specify query consistency?

Specifying the query consistency mode can be done either by the client sending the request, or using a server side policy.
If it isn't specified by either, the default mode of *strong* consistency applies.

### Specifying in client request properties

The query consistency mode can be set in the [client request properties](https://learn.microsoft.com/en-us/azure/data-explorer/kusto/api/netfx/request-properties){:target="_blank"}
of the query.

The name of the query option to set is `queryconsistency`, and the values to set are:

|Mode                             |Client request option value     |
|---------------------------------|--------------------------------|
|Strong                           | `strongconsistency`            |
|Weak (Random)                    | `weakconsistency`              |
|Weak (Affinitized by database)   | `weakconsistency_by_database`  |
|Weak (Affinitized by query text) | `weakconsistency_by_query`     |
|Weak (Affinitized by session ID) | `weakconsistency_by_session_id`|

When setting the `queryconsistency` option to `weakconsistency_by_session_id`, one should also set the query option named `query_weakconsistency_session_id` with a
string value that represents the session's ID.

A common mistake is to set the above properties as if they were boolean properties (e.g. `weakconsistency` = `true`, instead of `queryconsistency` = `weakconsistency`),
which doesn't have any impact, and retains the default mode of *strong* consistency.

### Specifying in a workload group's query consistency policy

The query consistency mode can be controlled on the server side, as part of a [Query consistency policy](https://learn.microsoft.com/en-us/azure/data-explorer/kusto/management/query-consistency-policy){:target="_blank"} at the [workload group](workload-groups.md){:target="_blank"} level.

Doing so can eliminate the need for users to specify the consistency mode in their client request properties, as well as provide the admin of the cluster with the ability
to prevent users from running with an undesired consistency mode.

The names of the value to in the query consistency policy are:

|Mode                             |Query consistency policy value  |
|---------------------------------|--------------------------------|
|Strong                           | `Strong`                       |
|Weak (Random)                    | `Weak`                         |
|Weak (Affinitized by database)   | `WeakAffinitizedByDatabase`    |
|Weak (Affinitized by query text) | `WeakAffinitizedByQuery`       |
|Weak (Affinitized by session ID) | `WeakAffinitizedBySessionId`   |

Setting `IsRelaxable` to `false` prevents the value set by the user in the client request properties to override the one that was set in the query consistency policy.

For example, the policy defined by the following control command, will result with:

1. All queries that get classified to the `default` workload group will run with weak consistency.
1. The consistency mode defined by the user in the client request properties is ignored.

```
.alter-merge workload_group default ```
{
  "QueryConsistencyPolicy": {
     "QueryConsistency": {
        "IsRelaxable": false,
        "Value": "Weak"
     }
  }
} ```
```

## Query consistency in cross-cluster queries

When you run a query on *cluster A*, that invokes a remote query on *cluster B*, the effective consistency mode is the one that was determined on *cluster A*, unless it gets overriden on
*cluster B*.

For example, if the query consistency was determined as `weakconsistency`, due to the query consistency policy defined on the workload group in *cluster A*, then the sub-query to cluster
B will be sent with the same `weakconsistency`. If the query consistency policy defined on the workload group in *cluster B* overiddes

---

{% include  share.html %}
