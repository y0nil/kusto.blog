---
title: Data partitioning
---
# Data partitioning in Kusto (Azure Data Explorer)

*Last modified: 05/14/2021*

By default, tables in Kusto are partitioned according to the time at which data is ingested. In the majority of use cases, there is no need to change that, unlike in other technologies, in which data partitioning is necessary in many cases, to reach better performance.

In specific cases, it is possible and recommended to define a [Data partitioning policy](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/management/partitioningpolicy){:target="_blank"}, to reach improved performance at query time.

* TOC
{:toc}

## How data partitioning works

- When data is ingested into a table in a Kusto database, [data shards (extents)](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/management/extents-overview){:target="_blank"} are created.
- To maintain query efficiency, smaller extents are merged into larger ones.
  - That is done automatically, as a background process.
  - It reduces the management overhead of having a large number of extents to track.
  - It also allows Kusto to optimize its indexes and improve compression.
- When a partitioning policy is defined on a table, extents participate in another background process *after* they're created (ingested), and *before* they are merged.
  - This process re-ingests the data from the source extents and creates *homogeneous* extents.
  - In these, all values in the column that is the table's partition key belong to the same partition.
  - Extents that belong to different partitions do not get merged together.
- Because extents are not merged before they are *homogeneous*, it is important to make sure the cluster's maximum [capacity](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/management/capacitypolicy){:target="_blank"} for partitioning and for merges are balanced, so that:
  - The rate of creating new extents isn't significantly higher than the rate of merging them.
  - The overall number of extent in the database/cluster doesn't keep growing significantly over time.
- Because the partitioning process requires reading and re-ingesting the data again, the process is expected to have a continuous overhead on the cluster's resources utilization.
- In certain cases, this means that increasing [the size of the cluster](https://docs.microsoft.com/en-us/azure/data-explorer/manage-cluster-choose-sku){:target="_blank"} would be required.
- The partitioning process prioritizes the table with a data partitioning policy that has the largest amount of non-partitioned records.
  - It is possible that partitioning data in one very large table may delay partitioning data in other, smaller tables.

## When to use data partitioning

There are very specific scenarios in which the benefits of partitioning a table outweigh the overhead of resources being continuously invested in the process. In such cases, the upside is expected to be significant, and improve query performance several times. In [other cases](#when-not-to-use-data-partitioning), however, it can do more damage than good.

### Filtering

Homogeneous extents include metadata on their partition values, that allows Kusto's query planner to filter out data shards without having to perform an index lookup or scanning the data. As result, a significant reduction in resources required to serve queries is expected, when a query includes an equality filter on the partition key.

#### Example

- Table `T` has a `string`-typed column named `TenantId`, which represents a unique identifier for a tenant that the data in the table belongs to.
- `T` includes data for multiple tenants, say `10,000` or more.
- The majority of queries running against `T` use equality filters (`==` or `in()`) on the `TenantId` column.
  - For example:
    ```
    T 
    | where Timestamp > ago(30d)
    | where TenantId == 'f83a65e0-da2b-42be-b59b-a8e25ea3954c' // <--
    | where Severity == "Error"
    | summarize count() by bin(Timestamp, 1d)
    | render timechart
    ```
  - The value `f83a65e0-da2b-42be-b59b-a8e25ea3954c` belongs to a single partition, out of the maximum number of partitions defined in the policy (for example: partition number `10` out of a total of `256`).
- The filter on `TenantId` is highly efficient, as it allows Kusto's query planner to filter out any extents that belongs to partitions that aren't partition number `10`.
  - Assuming equal-distribution of data across tenants in `T`, that means we're left with 1/256 (~0.39%) of the extents, even before evaluating the [datetime pre-filter](datetime-columns.md) and leveraging the default index on `TenantId`.
- When the amount of concurrent queries is higher (dozens or more), the benefit increases significantly - as each such query consumes less resources.
- In this sample use case, it's appropriate to set the partition assignment mode for the hash partition key to `Uniform`.

### Joins / aggregations

When a table has a hash partition key defined in its partitioning policy, there are two different assignment modes:
- `Default`: All homogeneous extents that belong to the *same* partition are assigned to the *same* node in the cluster.
- `Uniform`: Extents' partition values are *not* taken into account when assigning extents *uniformly* to the cluster's nodes.

Choosing the `Default` mode makes sense when the cardinality of the hash partition key is very high, partitions are expected to be ~equally-sized, and the query pattern uses the [shuffle strategy](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/query/shufflequery){:target="_blank"} often.
- Queries that use the `shuffle` strategy, and in which the shuffle key used in `join`, `summarize` or `make-series` is the table's hash partition key - are expected to perform better. This is because the amount of data required to move across the cluster's nodes is significantly reduced.

#### Example

- Table `T` has a `string`-typed column named `DeviceId`, which represents a unique identifier for a device that the data in the table was sent from.
- 10s of millions of such devices are spread across the world, and they all emit a similar amount of metrics per unit of time.
- Queries running against `T` aggregate over `DeviceId` using the `shuffle` strategy, with `DeviceId` as the shuffle key.
  - For example:
  ```
  T
  | where Timestamp > ago(1d)
  | summarize hint.shufflekey = DeviceId arg_max(Temperature, RelativeHumidity) by DeviceId // <--
  | where RelativeHumidity > 75
  | count
  ```

### Backfill or unordered ingestion

In many systems, data is ingested close to when it was generated at its source. More than that - data is ingested in-order, e.g. data from 10 minutes ago is ingested after data from 15 minutes ago. There are some cases, however, in which the patterns are different - these are the ones referred to in this section.

#### Example

- Data can be spread across source files according to a different partition key, and not according to time.
- For example: A single file may include all records for a single division, however those records span a period of 3 years.
- This data includes a datetime column named `record_timestamp` and is ingested into table `T`.
- When this data is ingested into `T`, the creation time of the result extents is set to the time of ingestion, and has no relationship with the datetime values in the dataset.
  - As the source files are mixed, the ingestor can't solve this by specifying the [CreationTime property](/advanced-data-management.md#back-filling-data){:target="_blank"} at ingestion time.
- Data [retention](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/management/retentionpolicy){:target="_blank"} and [caching](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/management/cachepolicy){:target="_blank"} policies act according to the extents' creation times. A different way is required for overriding it, based on the values of `records_timestamp`.
- In this case, setting `record_timestamp` as the uniform range datetime partition key of `T`, while specifying the `OverrideCreationTime` property as `true` will have the cluster re-partition the data and organize the records 
- And, if the table has an effective caching policy of 30 days, any records older than that period will not be cached.

### Data compression

Data partitioning results with similar values of the partition key ending up in the same data shards. In some cases, data in other columns behaves similarly, as values sent from the same device or tenant have similar characteristics, compared to those sent from others.

As a result, in many cases the compression ratio of such columns, and not only the partition key, improves. Meaning - less storage is required to store the data, and a potential cost reduction is achievable.

As different data sets may have different characteristics - YMMV.

## When *not* to use data partitioning

Any other case which doesn't meet the criteria mentioned above will not benefit, and may be harmed by enabling data partitioning.

Specifically, **don't** use data partitioning when:
- You don't frequently use equality filters or join/aggregate on the chosen hash partition key.
- Your data is unevenly distributed across the partitions defined by your policy, and you want each partition to be assigned to a single node.
  - If data is already ingested into a non-partitioned table in Kusto, an easy way to check this is by running a query such as the following, and making sure the values under the `count_` column are ~even:
  ```
  T datascope=hotcache
  | summarize count() by p = hash(my_column, 256)
  | order by count_ desc
  ```
- The cardinality of the chosen hash partition key is low.
  - For example: Table `T` has a column `Severity` with only 5 unique values ("Verbose", "Info", "Warning", "Error", "Critical"), even if you frequently filter on this column.
- You have many outliers in your data, that will result with a large amount of small extents.
  - For example: you defined a uniform range datetime partition key, with a range of 1 day, but there are many records with 'bad' datetime values in the column from the distant past/future (e.g. `0003-12-31` and `3020-07-11`).

More importantly - make sure you:
- Follow the guidelines in the [documentation](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/management/partitioningpolicy){:target="_blank"}
- Monitor & measure the impact of the policies you set on the cluster.

## Frequently asked questions

***1. The vast majority of my queries look at data that was ingested in the last 30 minutes or less. Should I define a partitioning policy on my table?***

No. The partitioning process runs after ingestion, and may not be able to partition all of the recently ingested data within a short period (e.g. 30 minutes or less).

***2. The cardinality of my candidate column for a hash partition key is low (e.g 10 values). Should I define a partitioning policy on my table?***

No. With lower cardinalities, the benefits of re-partitioning the data decrease significantly, and aren't likely to overweigh the overhead of the background process.

***3. I have a large backlog (e.g. multi-terabytes) of data that is already ingested from a while back. Should I set the `EffectiveDateTime` property in the partitioning policy to include it?***

It is recommended to focus on newly ingested data, that hasn't yet been merged into larger data shards - those are likely to take much longer to re-partition, and that may impact
partitioning of newly-ingested data in other tables in the database.

It is possible to re-partition old data, however if you do choose to partition historical data, consider doing so gradually - i.e. set the `EffectiveDateTime` to a previous
datetime in steps of up to a few days each time you alter the policy, while [monitoring](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/management/partitioningpolicy#monitor-partitioning){:target="_blank"}
the health of the process before moving on to the next step.

***4. I'm importing a large data set stored in a different database technology, where it had a partitioning policy defined. Should I use the same partition keys in Kusto?***

Not necessarily. By default, tables in Kusto are partitioned according to the time at which data is ingested. In the majority of use cases, there is no need to change that, unlike in other technologies, in which data partitioning is necessary in many cases, to reach better performance.

The specific scenarios in which data partitioning in Kusto should be considered are detailed [above](#when-to-use-data-partitioning).

***5. Is there a way for me to see the percentage of partitioned records across _all_ tables in my database, and not just the table with the minimal percentage (for which I use `.show diagnostics`)?***

Yes. You may use the following command - However, don't do so frequently, as it is not lightweight.

```
.show database DATABASE_NAME extents details
| summarize TotalRecords = sum(RowCount),
            PartitionedRecords = sumif(RowCount, isnotempty(Partition))
         by TableName
| extend percentage = round(100.0 * PartitionedRecords / TotalRecords, 2)
```

***6. Using `.show commands`, I'm seeing that commands of kind `ExtentsPartition` are taking long to complete and are consuming a very high amount of memory and CPU. Is there something I can do to improve the runtime of each command?***

This could be a result of too many records / too much data in the source extents being processed as part of a single command. The default targets (5M records / 5GB of original size) may be too high in certain cases, e.g.
very wide tables (100s of columns).

In such cases, you can set lower limits for the following properties in the data partitioning policy: `MaxRowCountPerOperation`, `MaxOriginalSizePerOperation`
([documentation](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/management/partitioningpolicy#additional-properties){:target="_blank"}).

Furthermore - for a hash partition key, decreasing the value set for `MaxPartitionCount` could help reduce the overhead of the partitioning process, though at some cost to the benefits at query runtime.
The same goes for the `Range` of a uniform range datetime partition key.

---

{% include  share.html %}