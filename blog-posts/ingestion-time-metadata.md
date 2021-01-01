---
title: Specifying metadata at ingestion time
---

{% include  share.html %}

---

# Specifying metadata at ingestion time in Kusto (Azure Data Explorer)

*Last modified: 12/21/2018*

It's simple to get the time of ingestion for each record that gets ingested into your Kusto table, by verifying the table's [ingestion time policy](https://docs.microsoft.com/en-us/azure/kusto/management/ingestiontimepolicy){:target="_blank"} is enabled,
and using the [ingestion_time()](https://docs.microsoft.com/en-us/azure/kusto/query/ingestiontimefunction){:target="_blank"} function at query time.

In some cases, you may want to include additional metadata at record level, for instance - the name of the source file/blob the record originated from,
the version of the client application which ingested the data, etc.

Kusto's [data ingestion mappings](https://docs.microsoft.com/en-us/azure/kusto/management/mappings){:target="_blank"} make this simple to achieve, by supporting the option of specifying constant values, which will apply to all the records being
ingested in the same batch.

## Is it really that simple?

Yes. Let's look at an example:

1. I've created a table named `MyTable` in my database, using the following command:
    ```
    .create table MyTable (Name:string, Hits:int, Timestamp:datetime)
    ```
    As you can see, the table has 3 different columns, of three different data types.

2. I've prepared 2 CSV files which I plan to ingest into my table:

    - `data_1.csv` was created by an application named `HisApp`, and has the following content:

        ```
        Jack,7,2018-12-21 00:11
        John,16,2018-12-22 23:44
        Fred,3,2018-12-18 14:55
        ```
    - `data_2.csv` was created by an application named `HerApp`, and has the following content:
        ```
        Jill,37,2018-12-21 11:23
        Jane,9,2018-12-20 04:32
        Shannon,13,2018-12-1 19:43
        ```
3. When I query `MyTable`, I want to be able to filter or aggregate by records which were ingested by
a specific app, and/or according to the name of the source files which included the records.
In order to do so, I'll use the following CSV mapping when I ingest the files:

    ```
    [    
        { "Name": "Name",            "Ordinal": 0 },
        { "Name": "Hits",            "Ordinal": 1 },
        { "Name": "Timestamp",       "Ordinal": 2 },
        { "Name": "FileName",        "ConstValue": "HerApp"}
        { "Name": "ApplicationName", "ConstValue": "data_2.csv"}
    ]
     ```

    And for each of my ingestion commands, I'll make sure I specify the appropriate `ConstValue` for both
    the `FileName` and `ApplicationName` fields.
    
4. I'll run the following 2 ingestion commands (note that the constant values in the mappings are
different in both):

    ```
    .ingest into MyTable (@'https://yonisstorage.blob.core.windows.net/samples/data_1.csv') with (format='csv', csvMapping='[{ "Name": "Name", "Ordinal": 0 }, { "Name": "Hits", "Ordinal": 1 }, { "Name": "Timestamp", "Ordinal": 2 }, { "Name": "FileName", "ConstValue": "data_1.csv"}, { "Name": "ApplicationName", "ConstValue": "HisApp"}]')
    ``` 
    ```
    .ingest into MyTable (@'https://yonisstorage.blob.core.windows.net/samples/data_2.csv') with (format='csv', csvMapping='[{ "Name": "Name", "Ordinal": 0 }, { "Name": "Hits", "Ordinal": 1 }, { "Name": "Timestamp", "Ordinal": 2 }, { "Name": "FileName", "ConstValue": "data_2.csv"}, { "Name": "ApplicationName", "ConstValue": "HerApp"}]')
    ```
5. Here are the contents of my table after both of these completed successfully:

    ```
    | Name    | Hits | Timestamp        | FileName   | ApplicationName |
    |---------|------|------------------|------------|-----------------|
    | Jack    | 7    | 2018-12-21 00:11 | data_1.csv | HisApp          |
    | John    | 16   | 2018-12-22 23:44 | data_1.csv | HisApp          |
    | Fred    | 3    | 2018-12-18 14:55 | data_1.csv | HisApp          |
    | Jill    | 37   | 2018-12-21 11:23 | data_2.csv | HerApp          |
    | Jane    | 9    | 2018-12-20 04:32 | data_2.csv | HerApp          |
    | Shannon | 13   | 2018-12-1 19:43  | data_2.csv | HerApp          |
    ```

6. Now, I can use these new columns, which did not originally exist in my data, in my queries.

    For example:
    ```
    MyTable
    | summarize count() by FileName
    ```
    Will result with:
    ```
    | FileName   | count_ |
    |------------|--------|
    | data_1.csv | 3      |
    | data_2.csv | 3      |
    ```
    And:
    ```
    MyTable
    | where FileName has 'data_1'
    | top 2 by Hits desc
    | project Name, Hits
    ```
    Will result with:
    ```
    | Name | Hits |
    |------|------|
    | John | 16   |
    | Jack | 7    |
    ```
    
## Notes
- In step #1 chose to create the table upfront, but as I'm using an ingestion mapping in my commands, I could have let the table be automatically created.
- I didn't include the extra columns (`FileName`, `ApplicationName`) in my original table's definition.
That's OK - they will be automatically appended to my table's schema, as my ingestion commands include
an ingestion mapping.
- If your source data is formatted as JSON, a [JSON mapping](https://docs.microsoft.com/en-us/azure/kusto/management/mappings#json-mapping){:target="_blank"} will allow you to specify 2 special transformations: `SourceLocation` and `SourceLineNumber`, which
enable you to enrich your records with both the name of the file that included the record, and the line number of that record in the source file.

---

{% include  share.html %}