---
title: Ingesting 2 billion NYC taxi rides
---
# Ingesting 2 billion new york city taxi rides into Kusto

*Last modified: 04/24/2023*

The [NYC Taxi & Limousine Commission](https://www1.nyc.gov/site/tlc/index.page){:target="_blank"} makes
historical data about taxi trips and for-hire-vehicle trips (such as [Uber](analyzing-uber-rides-history.md){:target="_blank"},
Lyft, Juno, Via, etc.) [available](https://www1.nyc.gov/site/tlc/about/tlc-trip-record-data.page){:target="_blank"}
for anyone to download and analyze. These records capture pick-up and drop-off dates/times, pick-up and drop-off locations,
trip distances, itemized fares, rate types, payment types, and driver-reported passenger counts.

<p align="center">
  <img title="Ingesting 2 Billion New York City Taxi rides into Kusto" src="../resources/images/nyc-taxi-theme.png">
</p>

[Todd W. Schneider](https://github.com/toddwschneider){:target="_blank"} took this data to the next level, and in his
[nyc-taxi-data GitHub repo](https://github.com/toddwschneider/nyc-taxi-data){:target="_blank"} made it easy to import
the data into [PostgreSQL](https://www.postgresql.org/){:target="_blank"}, and then use [PostGIS](https://postgis.net/){:target="_blank"}
for doing spatial calculations over it.

At the time of writing this post (February 2019), the available data set included:

* 2.06 billion total trips
    * ~ 1.5 billion taxi, from 2009-01-01 to 2018-06-30.
    * ~ 0.5 billion for-hire vehicle, from from 2015-01-01 to 2018-06-30.

This is a fair amount of records, and for getting it ingested and analyzed quickly, I made the natural choice of using **Kusto**.
After preparing the data set in PostgreSQL, I easily exported it to blobs in CSV format, and made it
available for Kusto to consume.

This post covers ingestion of the data into Kusto, while [another post](analyzing-nyc-taxi-rides.md){:target="_blank"}
covers analyzing the data, post-ingestion.

* TOC
{:toc}

## Ingesting 1.55 Billion Taxi Trips

### Preparing the data set

To prepare this data set, I mostly followed the instructions by [Todd W. Schneider](https://github.com/toddwschneider){:target="_blank"}
on his [nyc-taxi-data GitHub repo](https://github.com/toddwschneider/nyc-taxi-data){:target="_blank"}. As the process is a little
tricky and time consuming (using PostgreSQL on a single virtual machine), I've included a section with a few tips at the bottom of this post:
[Appendix: Tips for preparing the Yellow/Green Taxi trips data set](#appendix-tips-for-preparing-the-yellowgreen-taxi-trips-data-set).

**Update:** the enriched data set I used is now available in a public Azure blob storage container:
[https://kustosamplefiles.blob.core.windows.net/taxirides](https://kustosamplefiles.blob.core.windows.net/taxirides)

### Ingesting the files from Azure blob storage

Once the data set was prepared in Azure blob storage, the easy part was getting it into Kusto.
First, I created the table with a schema which matches the data I exported from PostgreSQL:

```
.create table Trips (
    trip_id:long,
    vendor_id:string,
    pickup_datetime:datetime,
    dropoff_datetime:datetime,
    store_and_fwd_flag:string,
    rate_code_id:int,
    pickup_longitude:real,
    pickup_latitude:real,
    dropoff_longitude:real,
    dropoff_latitude:real,
    passenger_count:int,
    trip_distance:real,
    fare_amount:real,
    extra:real,
    mta_tax:real,
    tip_amount:real,
    tolls_amount:real,
    ehail_fee:real,
    improvement_surcharge:real,
    total_amount:real,
    payment_type:string,
    trip_type:int,
    pickup:string,
    dropoff:string,
    cab_type:string,
    precipitation:int,
    snow_depth:int,
    snowfall:int,
    max_temperature:int,
    min_temperature:int,
    average_wind_speed:int,
    pickup_nyct2010_gid:int,
    pickup_ctlabel:string,
    pickup_borocode:int,
    pickup_boroname:string,
    pickup_ct2010:string,
    pickup_boroct2010:string,
    pickup_cdeligibil:string,
    pickup_ntacode:string,
    pickup_ntaname:string,
    pickup_puma:string,
    dropoff_nyct2010_gid:int,
    dropoff_ctlabel:string,
    dropoff_borocode:int,
    dropoff_boroname:string,
    dropoff_ct2010:string,
    dropoff_boroct2010:string,
    dropoff_cdeligibil:string,
    dropoff_ntacode:string,
    dropoff_ntaname:string,
    dropoff_puma:string
)
```

For ingestion, I chose using [LightIngest](https://learn.microsoft.com/en-us/azure/data-explorer/lightingest){:target="_blank"} - a simple command line utility
I find very useful and simple to use, if you want to some ad-hoc ingestion.

All I need to know is:
* The name of my database (`TaxiRides`)
* The name of my table (`Trips`)
* The name and region for my data management cluster (`ingest-myclustername.region`)
* The path to my Azure blob container (`https://kustosamplefiles.blob.core.windows.net/taxirides`)
* The format my files were created with (`CSV`, Gzip-compressed).

And then I run the command:

```
LightIngest.exe
   https://ingest-<myclustername>.<region>.kusto.windows.net;fed=true
   -database:TaxiRides
   -table:Trips
   -source:https://kustosamplefiles.blob.core.windows.net/taxirides
   -pattern:*.csv.gz
   -format:csv
```

### Measuring ingestion duration

On the client side, this runs in a matter of seconds, as it only queues the files for asynchronous ingestion (read more
[here](https://docs.microsoft.com/en-us/azure/kusto/api/netfx/about-kusto-ingest#queued-ingestion){:target="_blank"}).

*How long did it take the service to ingest these 1548 files with 1.55 billion records?*

I ran this with 2 different configurations, to demonstrate Kusto's ability to scale
its ingestion capacity, depending on the number and kind of nods the cluster has.

I ran the ingestion of the same set of blobs twice, while
[changing the number of the instances my cluster had](https://learn.microsoft.com/en-us/azure/data-explorer/manage-cluster-horizontal-scaling){:target="_blank"})
in between:
* 2 X `D14_v2`, with a table named `Trips`
* 6 X `D14_v2`, with a table named `Trips2`
    * These VMs have 16 vCPUs and 112GB of RAM.

Then, I can simply ask the service, using either of the following options, how long it took for each case:

Using the [.show commands](https://docs.microsoft.com/en-us/azure/kusto/management/commands){:target="_blank"} command:

```
.show commands 
| where StartedOn > datetime(2019-02-04 06:00)
| where CommandType == "DataIngestPull"
| where Text contains 'into Trips'
| parse Text with * "into " TableName " (" *
| extend ClusterSize = case(TableName == "Trips", "2xD14_v2",
                            TableName == "Trips2", "6xD14_v2",
                            "N/A")
| summarize ['# Commands'] = count(), 
            StartTime = min(StartedOn), 
            EndTime = max(LastUpdatedOn)
            by ClusterSize,
            CommandType, 
            State
| extend Duration = EndTime - StartTime
```

| ClusterSize | Duration         | CommandType    | State     | # Commands | StartTime                   | EndTime                     |
|-------------|------------------|----------------|-----------|------------|-----------------------------|-----------------------------|
| 2xD14_v2    | 00:47:33.2867817 | DataIngestPull | Completed | 1417       | 2019-02-04 06:00:39.4265609 | 2019-02-04 06:48:12.7133426 |
| 6xD14_v2    | 00:20:25.5162013 | DataIngestPull | Completed | 1415       | 2019-02-08 03:34:09.6342569 | 2019-02-08 03:54:35.1504582 |


Or, using the [ingestion_time()](https://docs.microsoft.com/en-us/azure/kusto/query/ingestiontimefunction){:target="_blank"} function:

```
union withsource=TableName Trips, Trips2
| where pickup_datetime between(datetime(2009-01-01) .. datetime(2018-07-01))
| summarize 
    TotalTrips = count(),
    EarliestTrip = min(pickup_datetime),
    LatestTrip = max(pickup_datetime),
    IngestionDuration = max(ingestion_time()) - min(ingestion_time())
by TableName 
| extend ClusterSize = case(TableName == "Trips", "2xD14_v2",
                            TableName == "Trips2", "6xD14_v2",
                            "N/A")
| project-away TableName
```

| ClusterSize | IngestionDuration | TotalTrips | EarliestTrip                | LatestTrip                  |
|-------------|-------------------|------------|-----------------------------|-----------------------------|
| 2xD14_v2    | 00:46:57.8493213  | 1547471140 | 2009-01-01 00:00:00.0000000 | 2018-07-01 00:00:00.0000000 |
| 6xD14_v2    | 00:19:54.1510651  | 1547471140 | 2009-01-01 00:00:00.0000000 | 2018-07-01 00:00:00.0000000 |


And as you can see, **it took only 20 minutes**, to ingest these 1,547,471,140 records, from 1548 source files, with 9.5 years' worth of data.
And they're now fully indexed and ready to query.

## Ingesting 0.5 Billion For-Hire-Vehicle Trips

To demonstrate how easy it is to use Kusto's client libraries to ingest data in
[supported formats](https://docs.microsoft.com/en-us/azure/kusto/management/data-ingestion/#supported-data-formats){:target="_blank"},
I chose taking this data set, in parquet format (with this [schema](https://www1.nyc.gov/assets/tlc/downloads/pdf/data_dictionary_trip_records_fhv.pdf){:target="_blank"})
and ingesting it using a Queued Ingestion client, which is available in
[Kusto's .NET client library](https://docs.microsoft.com/en-us/azure/kusto/api/netfx/about-kusto-ingest){:target="_blank"}. Needless to say, that C# is just one of the
languages in which the [client libraries](https://docs.microsoft.com/en-us/azure/kusto/api/){:target="_blank"}  are available.

Looking at the [NYC Taxi & Limousine Commission's site](https://www1.nyc.gov/site/tlc/about/tlc-trip-record-data.page){:target="_blank"}, it's easy to dynamically build
a list of URLs for these parquet files, and have them ingested from their source.

For the purpose of this ingestion I used:

* A Kusto cluster with 6 `D14_v2` nodes (it was over-provisioned for the purpose of the previous section, 
  I [scaled](https://docs.microsoft.com/en-us/azure/data-explorer/manage-cluster-scale-out){:target="_blank"} it down later).
* The [Microsoft.Azure.Kusto.Ingest](https://www.nuget.org/packages/Microsoft.Azure.Kusto.Ingest.NETStandard](https://www.nuget.org/packages/Microsoft.Azure.Kusto.Ingest){:target="_blank"} NuGet package.

Based on the schema provided on the site, I created the following table and ingestion mapping in my database:

```        
.create table FHV_Trips (
    dispatching_base_num:string,
    pickup_datetime:datetime,
    dropoff_datetime:datetime,
    pickup_location_id:int,
    dropoff_location_id:int,
    shared_ride_flag:bool
)

.create-or-alter table FHV_Trips ingestion parquet mapping "FHV_Trips_mapping" '['
  '{"Column": "dispatching_base_num", "Properties": {"Path": "$.dispatching_base_num"}},'
  '{"Column": "pickup_datetime",      "Properties": {"Path": "$.pickup_datetime"}},'
  '{"Column": "dropoff_datetime",     "Properties": {"Path": "$.dropOff_datetime"}},'
  '{"Column": "pickup_location_id",   "Properties": {"Path": "$.PUlocationID"}},'
  '{"Column": "dropoff_location_id",  "Properties": {"Path": "$.DOlocationID"}},'
  '{"Column": "shared_ride_flag",     "Properties": {"Path": "$.SR_Flag"}}'
']'
```

And here's the simple application I wrote and ran:

```csharp
public static void Main()
{
    var kustoConnectionStringBuilder = 
            new KustoConnectionStringBuilder(@"https://ingest-mycluster.region.kusto.windows.net")
            .WithAadApplicationKeyAuthentication(
                "<application id>",
                "<application key>",
                "<authority id>");

    var startTime = new DateTime(2015, 01, 01);
    var endTime = new DateTime(2018, 07, 01);
    
    var ingestionMapping = new IngestionMapping
    {
        IngestionMappingKind = Kusto.Data.Ingestion.IngestionMappingKind.Parquet,
        IngestionMappingReference = "FHV_Trips_mapping"
    };
    
    var ingestionProperties = new KustoIngestionProperties(databaseName: "TaxiRides", tableName: "FHV_Trips")
    {
        IngestionMapping = ingestionMapping
    };

    using (var ingestClient = KustoIngestFactory.CreateQueuedIngestClient(kustoConnectionStringBuilder))
    {
        for (var dt = startTime; dt < endTime; dt = dt.AddMonths(1))
        {
            var uri = $"https://d37ci6vzurychx.cloudfront.net/trip-data/fhv_tripdata_{dt.ToString("yyyy-MM")}.parquet";
            Console.WriteLine("Queueing file '{0}' for ingestion", uri);
            ingestClient.IngestFromStorage(uri, ingestionProperties);
        }
    }
}
```

### Measuring ingestion duration

On the client side, this runs in a matter of seconds, as it only queues the files for asynchronous ingestion (read more
[here](https://docs.microsoft.com/en-us/azure/kusto/api/netfx/about-kusto-ingest#queued-ingestion){:target="_blank"}).

*How long did it take the service to ingest these 42 files with 0.5 billion records?*

I can simply ask the service, using either of the following options:

Using the [.show commands](https://docs.microsoft.com/en-us/azure/kusto/management/commands){:target="_blank"} command:
    
```
.show commands 
| where StartedOn > datetime(2019-02-04 06:00)
| where CommandType == "DataIngestPull"
| where Text has '.ingest-from-storage async into FHV_Trips'
| summarize ['# Commands'] = count(), 
            StartTime = min(StartedOn), 
            EndTime = max(LastUpdatedOn)
| extend Duration = EndTime - StartTime
```

| Duration         | # Commands | StartTime                   | EndTime                     |
|------------------|------------|-----------------------------|-----------------------------|
| 00:02:35.0245767 | 21         | 2019-02-08 04:10:40.9281504 | 2019-02-08 04:13:15.9527271 |


Or, using the [ingestion_time()](https://docs.microsoft.com/en-us/azure/kusto/query/ingestiontimefunction){:target="_blank"} function:

```
FHV_Trips
| where pickup_datetime between(datetime(2009-01-01) .. datetime(2018-07-01))
| summarize 
    TotalTrips = count(),
    EarliestTrip = min(pickup_datetime),
    LatestTrip = max(pickup_datetime),
    IngestionDuration = max(ingestion_time()) - min(ingestion_time())
```

| IngestionDuration | TotalTrips | EarliestTrip                | LatestTrip                  |
|-------------------|------------|-----------------------------|-----------------------------|
| 00:02:25.3214546  | 514304551  | 2015-01-01 00:00:00.0000000 | 2018-06-30 23:59:59.0000000 |


And as you can see, **it took only 2.5 minutes**, to ingest these 514,304,551 records, with 3.5 years' worth of data.
And they're now fully indexed and ready to query.

Now that I have all of this data ingested, [it's time to start analyzing it](analyzing-nyc-taxi-rides.md){:target="_blank"}.

---

## Appendix: Tips for preparing the Yellow/Green Taxi trips data set

In case you're going to perform this on an Azure VM, you may find the following tips useful.

**Note**: These are  not related to Kusto, but they may help you get the
data prepared, before ingesting it.

* I used an `Ubuntu Server 16.04 LTS` [D5_v2](https://docs.microsoft.com/en-us/azure/virtual-machines/linux/sizes-general#dv2-series){:target="_blank"} virtual machine.

    ![](../resources/images/ubuntu-16-04.png)

* You should have at least 700GB of storage available for the raw data and PostgreSQL database, and you should prefer that to be backed by a SSD.

* I made sure my PostgreSQL instance's data location was on the larger SSD and not where it is
  by default, which is the OS drive. I did so by following the useful instructions
  [here](https://www.digitalocean.com/community/tutorials/how-to-move-a-postgresql-data-directory-to-a-new-location-on-ubuntu-16-04){:target="_blank"}.

* Once the database was ready, I exported it to CSV files with GZip compression, using the [COPY command](https://www.postgresql.org/docs/9.2/sql-copy.html){:target="_blank"}.
    * I chose to generate files with 1,000,000 records each, so I ended up with 1,548 `*.csv.gz` files.
    * I made sure I deleted the source CSV files from the SSD before starting the `COPY`, to have enough available disk space.

* Once the export was complete, I used Azure CLI for uploading the files to an Azure storage blob container.
    * I installed it using the instructions provided [here](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli-apt?view=azure-cli-latest){:target="_blank"}.
    * I uploaded the entire local directory to my container using the
      [az storage blob upload-batch](https://docs.microsoft.com/en-us/cli/azure/storage/blob?view=azure-cli-latest#az-storage-blob-upload-batch){:target="_blank"} command.

* The process is long - It took approximately 2 days to run on my VM.
    * If you're not interested in enriching the original data set, you might as well ingest it directly from the source (like I did for the [FHV trips](#ingesting-05-billion-for-hire-vehicle-trips)).

---

{% include  share.html %}
