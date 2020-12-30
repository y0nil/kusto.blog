---
title: Advanced data management in Kusto (Azure Data Explorer)
---
**[Go back home](../index.md)**

{% include  share.html %}

---

# Advanced data management in Kusto (Azure Data Explorer)

*Last modified: 11/19/2018*

The following sections describe techniques and practices which you may find useful for your day-to-day data ingestion and data management flows.

* TOC
{:toc}

## Committing multiple bulks of data in a single transaction

At times, you may want to ingest large bulks of data, but make them available to query in a single transaction, only when all bulks have been ingested successfully.

For this purpose, I can suggest the following techniques - one handles the case where you want to emplace (or replace) the contents of an entire table; the other handles the case where you want to do so for a subset of the contents of the table.

### Switching tables by renaming

An example use case could be when you want to ingest an entire table which contains a snapshot of data, and replace the previous snapshot of the data, without affecting the ability to query the table or accessing the previous "version" of the snapshot.

1. Let's assume you want the data to be queryable in a table named `T` (whether or not `T` exists before we begin).
2. You can create a new table, named `T_temp` (for example), which has the same schema as `T`.
    - A simple way to do so is using a [.set command](https://docs.microsoft.com/en-us/azure/kusto/management/data-ingestion/ingest-from-query){:target="_blank"}:
        ```
        .set T_temp <| T | limit 0
        ```
3. Ingest all your data into `T_temp`.
4. When your ingestion completes successfully, "switch" both tables using the [.rename tables](https://docs.microsoft.com/en-us/azure/kusto/management/tables#rename-tables){:target="_blank"} command:
    ```
    .rename tables T = T_temp, T_temp = T
    ``` 
5. Assuming the data that was originally in `T` is no longer of interest, simply drop the `T_temp` table using the [.drop table](https://docs.microsoft.com/en-us/azure/kusto/management/tables#drop-table){:target="_blank"} command.
6. Note that throughout your entire ingestion process and after it, the full data set in `T` remains available for queries, and only when you run the [.rename tables](https://docs.microsoft.com/en-us/azure/kusto/management/tables#rename-tables){:target="_blank"} command does the data get switched in a single transaction.

### Tagging and replacing extents (data shards)

Tagging [extents (data shards)](https://docs.microsoft.com/en-us/azure/kusto/management/extents-overview){:target="_blank"} can be a useful way to identify and handle bulks of data which share a common characteristic.

Ane example use case could be when you want to replace a subset of the data in your table (e.g. all the data from a specific day or week), without affecting the ability to query the table, or accessing the previous "version" of the data. 

1. Let's assume your table name it `T`.
2. You can create a new table, `T_temp` as shown in the previous section. This table will have the same schema as `T`.
3. Ingest all your data into `T_temp`, and specify unique [drop-by](https://docs.microsoft.com/en-us/azure/kusto/management/extents-overview#drop-by-extent-tags){:target="_blank"} tags when ingesting this data (so that in the future, you'll be able to identify and replace it, according to this tag).
    - Multiple ingestion methods allow you to the `tags` [ingestion property](https://docs.microsoft.com/en-us/azure/data-explorer/ingestion-properties){:target="_blank"}.
    - For this example, let's use `drop-by:2018-10-26` as our tag.
4. When all ingestion completes successfully, use the [.replace extents](https://docs.microsoft.com/en-us/azure/kusto/management/extents-commands#replace-extents){:target="_blank"} command to:
    1. Drop all [extents (data shards)](https://docs.microsoft.com/en-us/azure/kusto/management/extents-overview){:target="_blank"} with the relevant [drop-by](https://docs.microsoft.com/en-us/azure/kusto/management/extents-overview#drop-by-extent-tags){:target="_blank"} tags from `T`.
    2. Move all [extents (data shards)](https://docs.microsoft.com/en-us/azure/kusto/management/extents-overview){:target="_blank"} with the relevant [drop-by](https://docs.microsoft.com/en-us/azure/kusto/management/extents-overview#drop-by-extent-tags){:target="_blank"} tags from `T_temp` to `T`.
    - Here's how the command would look like:
        ```
        .replace extents in table T <| 
        { .show table T extents where tags has 'drop-by:2018-10-26' },
        { .show table T_Temp extents where tags has 'drop-by:2018-10-26' }
        ```
5. Both the *move* and *drop* operations are performed in a single transaction, so throughout your entire ingestion process and after it, the full data set in `T` remains available for queries.
6. Assuming the tagged data shards that were originally in `T` are no longer of interest, simply drop the `T_temp` table using the [.drop table](https://docs.microsoft.com/en-us/azure/kusto/management/tables#drop-table){:target="_blank"} command. Or, if you have additional flows utilizing it for the same purpose in parallel - drop the specific [extents (data shards)](https://docs.microsoft.com/en-us/azure/kusto/management/extents-overview){:target="_blank"} from it, using the [.drop extents](https://docs.microsoft.com/en-us/azure/kusto/management/extents-commands#drop-extents){:target="_blank"} command.

> **Important Note:** there could be a downside to over-using extent tagging (e.g. if you tag each ingestion operation with a unique [drop-by](https://docs.microsoft.com/en-us/azure/kusto/management/extents-overview#drop-by-extent-tags){:target="_blank"} tag). Make sure you're familiar with the performance notes in [this document](https://docs.microsoft.com/en-us/azure/kusto/management/extents-overview#extent-tagging){:target="_blank"}. If you do find you use them excessively, it's recommended you use the [.drop extent tags](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/management/drop-extent-tags){:target="_blank"} command to remove tags which are no longer required.

### Moving extents (data shards) between tables

If, however, you're simply in need of adding new data to your table, without replacing or dropping existing data, but you still want all the new data to become available to queries at once, I would suggest:
1. Ingesting into a "temporary" table (`T_temp`)
2. Using the [.move extents](https://docs.microsoft.com/en-us/azure/kusto/management/extents-commands#move-extents){:target="_blank"} command, after all ingestions have completed, to move the newly created extents (data shards) from `T_temp` to your target table (`T`).

### Background processes and how they affect moving and replacing extents

As you may have read [here](https://docs.microsoft.com/en-us/azure/kusto/management/extents-overview){:target="_blank"}, an extent's lifetime may include it getting merged or rebuilt with other extents, to reach some optimum size. Such operations, as well as operation run for enforcing the [data retention policy]({:target="_blank"}){:target="_blank"}, are run in the background of the cluster, and can at times conflict with other operations that you run at extent level, include [moving](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/management/move-extents){:target="_blank"} and [replacing](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/management/replace-extents){:target="_blank"} extents.

The two latter work in an "all-or-nothing" manner, meaning that in case one of the source extents that existed at the beginning of the operation no longer exists when the operation is about to be committed, the entire `.move` or `.replace` operation will fail. This can happen, for example, if the source extent was dropped due to retention, or merged with another extent, as part of another parallel operation.

It's important that you handle such potential failure cases accordingly, by interpreting the output of the commands correctly, and be ready to retry the operation in cases where it makes sense to do so.

- The output of both commands is documented [here](https://docs.microsoft.com/en-us/azure/kusto/management/extents-commands){:target="_blank"}.

- If you're running the command in `async` mode, simply follow its `State` when you run `.show operations <operation_id>` to see whether it succeeds or fails (there is no partial success, as explained above).
- Alterntively, when not running in `async` mode, you will get a more detailed result set back once the execution completes:

    - In case `ResultExtentId` is not `null` (which it will be, for extents being dropped as part of a `.replace` command), and it can't be parsed as a GUID, it means the extent failed to be moved, and the operation did not complete successfully (thus, none of the extents were moved).

## Back-filling data

In some cases, you want to ingest historical data into your table(s), however you still want it to be managed according to the policies (e.g. [retention policy](https://docs.microsoft.com/en-us/azure/kusto/management/retentionpolicy){:target="_blank"}, [caching policy](https://docs.microsoft.com/en-us/azure/kusto/management/cachepolicy){:target="_blank"}) you have defined.

For these cases, the `creationTime` [ingestion property](https://docs.microsoft.com/en-us/azure/data-explorer/ingestion-properties){:target="_blank"} is very useful. Multiple ingestion methods allow you to specify this ingestion property, with the effect of it overriding the [extent's (data shard's)](https://docs.microsoft.com/en-us/azure/kusto/management/extents-overview){:target="_blank"} creation time - This is a metadata property of the [extent (data shard)](https://docs.microsoft.com/en-us/azure/kusto/management/extents-overview){:target="_blank"}, according to which both [retention policy](https://docs.microsoft.com/en-us/azure/kusto/management/retentionpolicy){:target="_blank"} and [caching policy](https://docs.microsoft.com/en-us/azure/kusto/management/cachepolicy){:target="_blank"} policies are applied.

---

**[Go back home](../index.md)**

{% include  share.html %}