{% include  share.html %}

---

The posts below include examples and practices which combine the rich query and data management capabilities of **Kusto (Azure Data Explorer)**.
They will provide you with real-life best practices and methodologies, which have all been repeatedly proven in large-scale production environments,
and will help you make sure you make the most out of your Kusto cluster.

<p align="center">
  <img src="resources/images/adx-logo.png">
</p>

---

## **Queries**

- **[Just another Kusto hacker *(JAKH)*](blog-posts/jakh.md){:target="_blank"}**

- **[Analyzing Uber rides history in Kusto (Azure Data Explorer)](blog-posts/analyzing-uber-rides-history.md){:target="_blank"}**

- **[Analyzing Spotify streaming history in Kusto (Azure Data Explorer)](blog-posts/analyzing-spotify-streaming-history.md){:target="_blank"}**

- **[Analyzing 2 Billion New York City Taxi rides in Kusto (Azure Data Explorer)](blog-posts/analyzing-nyc-taxi-rides.md){:target="_blank"}**

## **Data Ingestion & Management**

- **[Data partitioning in Kusto (Azure Data Explorer)](blog-posts/data-partitioning.md){:target="_blank"}**

- **[Ingesting 2 Billion New York City Taxi rides into Kusto (Azure Data Explorer)](blog-posts/ingesting-nyc-taxi-rides.md){:target="_blank"}**

- **[Update policies for in-place ETL in Kusto (Azure Data Explorer)](blog-posts/update-policies.md){:target="_blank"}**

- **[Why filtering on datetime columns can save you time](blog-posts/datetime-columns.md){:target="_blank"}**

- **[Specifying metadata at ingestion time](blog-posts/ingestion-time-metadata.md){:target="_blank"}**

- **[Advanced data management in Kusto (Azure Data Explorer)](blog-posts/advanced-data-management.md){:target="_blank"}**
  - [Committing multiple bulks of data in a single transaction](blog-posts/advanced-data-management.md#committing-multiple-bulks-of-data-in-a-single-transaction){:target="_blank"}
  - [Back-filling data](blog-posts/advanced-data-management.md#back-filling-data){:target="_blank"}

---

## **Wait, let's start over - what is Kusto (Azure Data Explorer)?**

### **Haven't seen it in action yet?**

Start off with a couple of videos:

#### **Microsoft Ignite, Orlando, September 2018**

- [Scott Guthrie](https://www.linkedin.com/in/guthriescott){:target="_blank"}'s keynote - [watch on YouTube](https://www.youtube.com/watch?v=xnmBu4oh7xk&t=1h08m12s){:target="_blank"}
- [Rohan Kumar](https://www.linkedin.com/in/rohankumar){:target="_blank"}'s session - [watch on YouTube](https://www.youtube.com/watch?v=ZaiM89Z01r0&t=58m0s){:target="_blank"}
- [Manoj Raheja](https://www.linkedin.com/in/manoj-raheja-a02b2b32){:target="_blank"}'s introduction to Kusto (Azure Data Explorer) - [watch on YouTube](https://www.youtube.com/watch?v=GT4C84yrb68){:target="_blank"}

#### **Techorama, The Netherlands, October 2018**

- [Scott Guthrie](https://www.linkedin.com/in/guthriescott)'s keynote - [watch on YouTube](https://www.youtube.com/watch?v=YTWewM_UMOk&feature=youtu.be&t=3074){:target="_blank"}

#### **Microsoft //build, Seattle, May 2019**

- [Rohan Kumar](https://www.linkedin.com/in/rohankumar){:target="_blank"}'s session - [watch on YouTube](https://youtu.be/Fjfvz1HToek?t=2758){:target="_blank"}
- [Uri Barash](https://www.linkedin.com/in/uri-barash-7820594/){:target="_blank"}'s session - [watch on YouTube](https://youtu.be/chVFAGX8IYQ){:target="_blank"}

### **Customer stories**

- [Better forecasting, safer driving with Bosch](https://customers.microsoft.com/en-us/story/816933-bosch-automotive-azure-germany){:target="_blank"}
- [Data-driven Dodo Pizza raises performance with Azure Data Explorer](https://customers.microsoft.com/en-us/story/851838-dodo-pizza-consumer-goods-azure-en-russia){:target="_blank"}
- [Global consultancy Milliman powers award - winning Arius insurance reserving software using Azure Data Explorer](https://customers.microsoft.com/en-us/story/842088-milliman-insurance-azure-en-united-states){:target="_blank"}
- [Global software company Episerver uses Azure Data Explorer to gain enhanced consumer insight](https://customers.microsoft.com/en-us/story/817285-episerver-professional-services-azure-sweden){:target="_blank"}
- [Growing an innovative energy partnership across Australia](https://customers.microsoft.com/en-us/story/847171-agl-energy-azure-en-australia){:target="_blank"}
- [How Azure Data Explorer Helped Us Make Sense of 1M Log Lines per Second](https://engineering.taboola.com/azure-data-explorer-helped-us-make-sense-1m-log-lines-per-second/){:target="_blank"}
- [How Azure Data Explorer Was Able to Accelerate Namogoo’s Classification Processes 170X Faster](https://www.namogoo.com/blog/our-technology/how-azure-data-explorer-accelerates-namogoos-classification-processes-170x-faster/){:target="_blank"}
- [MFTBC's Truckonnect uses Azure Data Explorer to improve the customer experience and lower costs](https://customers.microsoft.com/en-us/story/850967-mitsubishi-fuso-automotive-azure){:target="_blank"}
- [Mobile user acquisition PaaS converts terabytes of real-time data into user engagement](https://customers.microsoft.com/en-us/story/816542-zoomd-technologies-azure-professional-services-israel-en){:target="_blank"}
- [NY fintech streamlines buy-side data analysis with Azure Data Explorer](https://customers.microsoft.com/en-us/story/825356-financial-fabric-banking-and-capital-markets-azure-powerbi-en-united-states){:target="_blank"}

---

### **Read more about it**

There's a lot to learn and read about. Start with the following links:

#### **October 2020 updates**

- [Azure Data Explorer - Re-imagine Telemetry Analytics (Uri Barash @ Microsoft Tech Community)](https://techcommunity.microsoft.com/t5/azure-data-explorer/azure-data-explorer-reimagine-telemetry-analytics/ba-p/1777362){:target="_blank"}
- [Online event summary](https://techcommunity.microsoft.com/t5/azure-data-explorer/azure-data-explorer-online-event-october-14-event-summary/ba-p/1793662){:target="_blank"}

#### **General availability announcement (February 2019)**

- [Azure Data Explorer - The fast and highly scalable data analytics service (
Jurgen Willis @ Microsoft Azure blog)](https://azure.microsoft.com/en-us/blog/individually-great-collectively-unmatched-announcing-updates-to-3-great-azure-data-services/){:target="_blank"}

#### **Public preview announcement (September 2018)**

- [Introducing Azure Data Explorer (Uri Barash @ Microsoft Azure blog)](https://azure.microsoft.com/en-us/blog/introducing-azure-data-explorer){:target="_blank"}
- [Azure Data Explorer Technology 101 (Ziv Caspi @ Microsoft Azure blog)](https://azure.microsoft.com/en-us/blog/azure-data-explorer-technology-101){:target="_blank"}
- [Azure Data Explorer: Whitepaper (Evgeney Ryzhyk @ Microsoft Azure resource center)](https://azure.microsoft.com/en-us/resources/azure-data-explorer){:target="_blank"}

#### **Official documentation**

- [Azure Data Explorer: Quickstarts and tutorials](https://docs.microsoft.com/en-us/azure/data-explorer){:target="_blank"}
- [Azure Data Explorer: Reference material](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/query/){:target="_blank"}

#### **StackOverflow questions**

- [kusto](https://stackoverflow.com/questions/tagged/kusto){:target="_blank"}
- [kusto-query-language](https://stackoverflow.com/questions/tagged/kusto-query-language){:target="_blank"}
- [kql](https://stackoverflow.com/questions/tagged/kql){:target="_blank"}
- [azure-data-explorer](https://stackoverflow.com/questions/tagged/azure-data-explorer){:target="_blank"}

---

### **Other stuff you should know**

### **Highlights from Kusto (Azure Data Explorer) documentation**

- [Creating a cluster and databases](https://docs.microsoft.com/en-us/azure/data-explorer/create-cluster-database-portal){:target="_blank"}
- [Query best practices](https://docs.microsoft.com/en-us/azure/kusto/query/best-practices){:target="_blank"}
- [Schema best practices](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/management/management-best-practices){:target="_blank"}
- [Ingestion best practices](https://docs.microsoft.com/en-us/azure/kusto/api/netfx/kusto-ingest-best-practices){:target="_blank"}

---

{% include  share.html %}
