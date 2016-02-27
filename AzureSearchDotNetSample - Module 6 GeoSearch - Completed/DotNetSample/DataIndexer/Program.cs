//Copyright Microsoft. All rights reserved.
//Licensed under the MIT License at http://mit-license.org/

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

// Data provided by U.S. Geological Survey - Department of the Interior/USGS
// http://geonames.usgs.gov/domestic/download_data.htm

using Microsoft.Azure;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Spatial;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace DataIndexer
{
    class Program
    {
        private static SearchServiceClient _searchClient;
        private static SearchIndexClient _indexClient;

        // This Sample shows how to delete, create, upload documents and query an index
        static void Main(string[] args)
        {
            string searchServiceName = ConfigurationManager.AppSettings["SearchServiceName"];
            string apiKey = ConfigurationManager.AppSettings["SearchServiceApiKey"];

            // Create an HTTP reference to the catalog index
            _searchClient = new SearchServiceClient(searchServiceName, new SearchCredentials(apiKey));
            _indexClient = _searchClient.Indexes.GetClient("geonames");

            Console.WriteLine("{0}", "Deleting index...\n");
            if (DeleteIndex())
            {
                Console.WriteLine("{0}", "Creating index...\n");
                CreateIndex();
                Console.WriteLine("{0}", "Sync documents from Azure SQL...\n");
                CreateAndSyncIndexer();
            }
            Console.WriteLine("{0}", "Complete.  Press any key to end application...\n");
            Console.ReadKey();
        }

        private static bool DeleteIndex()
        {
            // Delete the index if it exists
            try
            {
                _searchClient.Indexes.Delete("geonames");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error deleting index: {0}\r\n", ex.Message.ToString());
                Console.WriteLine("Did you remember to add your SearchServiceName and SearchServiceApiKey to the app.config?\r\n");
                return false;
            }

            return true;
        }

        private static void CreateIndex()
        {
            // Create the Azure Search index based on the included schema
            try
            {
                // Create the suggester for suggestions
                Suggester sg = new Suggester();
                sg.Name = "sg";
                sg.SearchMode = SuggesterSearchMode.AnalyzingInfixMatching;
                sg.SourceFields = new List<string>() { "FEATURE_NAME", "COUNTY_NAME" };


                var definition = new Index()
                {
                    Name = "geonames",
                    Fields = new[] 
                    { 
                        new Field("FEATURE_ID",     DataType.String)         { IsKey = true,  IsSearchable = false, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = true},
                        new Field("FEATURE_NAME",   DataType.String)         { IsKey = false, IsSearchable = true,  IsFilterable = true,  IsSortable = true,  IsFacetable = false, IsRetrievable = true},
                        new Field("FEATURE_CLASS",  DataType.String)         { IsKey = false, IsSearchable = true,  IsFilterable = true,  IsSortable = true,  IsFacetable = false, IsRetrievable = true},
                        new Field("STATE_ALPHA",    DataType.String)         { IsKey = false, IsSearchable = true,  IsFilterable = true,  IsSortable = true,  IsFacetable = true, IsRetrievable = true},
                        new Field("STATE_NUMERIC",  DataType.Int32)          { IsKey = false, IsSearchable = false, IsFilterable = true,  IsSortable = true,  IsFacetable = false,  IsRetrievable = true},
                        new Field("COUNTY_NAME",    DataType.String)         { IsKey = false, IsSearchable = true,  IsFilterable = true,  IsSortable = true,  IsFacetable = true, IsRetrievable = true},
                        new Field("COUNTY_NUMERIC", DataType.Int32)          { IsKey = false, IsSearchable = false, IsFilterable = true,  IsSortable = true,  IsFacetable = false,  IsRetrievable = true},
                        new Field("ELEV_IN_M",      DataType.Int32)          { IsKey = false, IsSearchable = false, IsFilterable = true,  IsSortable = true,  IsFacetable = true,  IsRetrievable = true},
                        new Field("ELEV_IN_FT",     DataType.Int32)          { IsKey = false, IsSearchable = false, IsFilterable = true,  IsSortable = true,  IsFacetable = true,  IsRetrievable = true},
                        new Field("MAP_NAME",       DataType.String)         { IsKey = false, IsSearchable = true,  IsFilterable = true,  IsSortable = true,  IsFacetable = false, IsRetrievable = true},
                        new Field("DESCRIPTION",    DataType.String)         { IsKey = false, IsSearchable = true,  IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = true},
                        new Field("HISTORY",        DataType.String)         { IsKey = false, IsSearchable = true,  IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = true},
                        new Field("LOCATION",       DataType.GeographyPoint) { IsKey = false, IsSearchable = false, IsFilterable = true,  IsSortable = false, IsFacetable = false, IsRetrievable = true},
                        new Field("DATE_CREATED",   DataType.DateTimeOffset) { IsKey = false, IsSearchable = false, IsFilterable = true,  IsSortable = true,  IsFacetable = true,  IsRetrievable = true},
                        new Field("DATE_EDITED",    DataType.DateTimeOffset) { IsKey = false, IsSearchable = false, IsFilterable = true,  IsSortable = true,  IsFacetable = true,  IsRetrievable = true}
                    },
                    Suggesters = new List<Suggester> { sg }
                };

                _searchClient.Indexes.Create(definition);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error creating index: {0}\r\n", ex.Message.ToString());
            }

        }

        private static void CreateAndSyncIndexer()
        {
            // Create a new indexer and sync it
            try
            {
                var creds = new DataSourceCredentials("Server=tcp:azs-playground.database.windows.net,1433;Database=usgs;User ID=reader;Password=EdrERBt3j6mZDP;Trusted_Connection=False;Encrypt=True;Connection Timeout=30");
                DataSource ds = new DataSource("usgs-datasource", DataSourceType.AzureSql, creds, new DataContainer("GeoNamesRI"));
                ds.Description = "USGS Dataset";

                Indexer idx = new Indexer();
                idx.Name = "usgs-indexer";
                idx.Description = "USGS data indexer";
                idx.DataSourceName = "usgs-datasource";
                idx.TargetIndexName = "geonames";
                idx.Parameters = new IndexingParameters();
                idx.Parameters.MaxFailedItems = 10;
                idx.Parameters.MaxFailedItemsPerBatch = 5;
                idx.Parameters.Base64EncodeKeys = false;

                //Delete indexer and datasource if it existed
                _searchClient.DataSources.Delete("usgs-datasource");
                _searchClient.Indexers.Delete("usgs-indexer");

                //Create indexer and datasource
                _searchClient.DataSources.Create(ds);
                _searchClient.Indexers.Create(idx);

                //Launch the sync and then monitor progress until complete
                bool running = true;

                Console.WriteLine("{0}", "Synchronization running...\n");
                while (running)
                {
                    IndexerExecutionInfo status = null;
                    try
                    {
                        status = _searchClient.Indexers.GetStatus(idx.Name);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error polling for indexer status: {0}", ex.Message);
                        return;
                    }

                    IndexerExecutionResult lastResult = status.LastResult;
                    if (lastResult != null)
                    {
                        switch (lastResult.Status)
                        {
                            case IndexerExecutionStatus.InProgress:
                                Console.WriteLine("{0}", "Synchronization running...\n");
                                Thread.Sleep(1000);
                                break;

                            case IndexerExecutionStatus.Success:
                                running = false;
                                Console.WriteLine("Synchronized {0} rows...\n", lastResult.ItemCount);
                                break;

                            default:
                                running = false;
                                Console.WriteLine("Synchronization failed: {0}\n", lastResult.ErrorMessage);
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error creating indexer: {0}: \n", ex.Message.ToString());
            }


        }
    }
}
