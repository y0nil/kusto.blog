---
title: DateTime columns
---
# DateTime columns in Kusto

*Last modified: 11/08/2018*

When you ingest batches of data into Kusto, it gets stored in a sharded column store, where each column is compressed and indexed.
The scope of each index is a single [data shard (extent)](https://docs.microsoft.com/en-us/azure/kusto/management/extents-overview){:target="_blank"}, and the implementation of the index depends on the type of the column.

For [datetime](https://docs.microsoft.com/en-us/azure/kusto/query/scalar-data-types/datetime){:target="_blank"} columns, the minimum and maximum values for each data shard are stored as part of the extent's metadata. This is done automatically for any datetime column, no user configuration is required.

With that information available, when you apply a datetime-filter in a query (e.g. `MyTable | where Timestamp > ago(1d)`), the query planner can identify which extents are  irrelevant for serving the query, and filters them out in advance, to avoid excessive scanning of indexes or data.

Thus, even if your table stores 1 year's worth of data (let's assume that's 100s of billions of records), when you query for last week's records - the irrelevant extents are filtered out upfront, and your query returns much faster compared to if it had to scan the entire data set.

The same is true if you want to query historical data, e.g. a specific week's worth of data from 6 months ago - only data shards which contain records from that week will be queried.

## Things to keep in mind

1. If your data includes a some kind of time representation which is not in a datetime format (e.g. epoch (`long`), ticks (`long`), or `string` etc.) - it is recommended that you either reformat your data so that it does include datetime values in a [supported format](https://docs.microsoft.com/en-us/azure/kusto/query/scalar-data-types/datetime#supported-formats){:target="_blank"}, or that you convert these values to datetime values at ingestion time (e.g., using an [update policy](update-policies.md)). In any case - your [table](https://docs.microsoft.com/en-us/azure/kusto/query/schema-entities/tables){:target="_blank"}'s schema should have this column defined with the `datetime` data type.
    - Here's one example on how to achieve such a conversion from a `long` value to a `datetime` value:
        ```
        let FromUnixTime = (t:long) { datetime(1970-01-01) + t * 1sec };
        print FromUnixTime(1537181567)
        ```  
        This returns `2018-09-17 10:52:47.0000000`.
    - And another example - converting from a `string` value to a `datetime` value:
        ```
        let FromString = (t:string) { todatetime(t) };
        print FromString("8 Nov 18 15:05:02 GMT")
        ```
        This returns `2018-11-08 15:05:02.0000000`.

2. If your data doesn't include datetime values, but you still want to be able to filter it according to when it got ingested, you can use the [ingestion_time() function](https://docs.microsoft.com/en-us/azure/kusto/query/ingestiontimefunction){:target="_blank"}, assuming the [ingestion time policy](https://docs.microsoft.com/en-us/azure/kusto/management/ingestiontimepolicy){:target="_blank"} is enabled on your table (which is true, by default).
    - When the policy is enabled, a hidden and internal datetime column (whose values are obtained using the `ingestion_time()` function) is added to the table, and it gets populated for each record that gets ingested into the table.

3. Due to the fact only the minimum and maximum values for datetime column are stored in the extent's metadata (as previously explained), if you have skewed values *from the past or future* being ingested alongside ones from the present, this can limit the ability to pre-filter irrelevant extents at query planning time.
    - In case you're back-filling historical data - have a look at [this post](advanced-data-management.md#back-filling-data), which refers to the `creationTime` ingestion property.
    - If you're not sure whether or not your datetime values are skewed, a query like the following one can help you figure that out:
        ```
        MyTable
        | where ingestion_time() > ago(3h)
        | summarize count() by bin(DateTimeColumnIWantToCheck, 1h)
        ```
        If my data is OK, I'd expect to get 3 or 4 hourly buckets (depending on when I run the query). For instance, if run the query above at `2018-11-09 05:17:31`, I'll get:
        
        |DateTimeColumnIWantToCheck  |count_        |
        |----------------------------|--------------|
        |2018-11-09 05:00:00.0000000 |1,707,930,236 |
        |2018-11-09 04:00:00.0000000 |8,363,683,151 |
        |2018-11-09 03:00:00.0000000 |8,353,791,287 |
        |2018-11-09 02:00:00.0000000 |6,545,498,120 |
        
    
---

{% include  share.html %}