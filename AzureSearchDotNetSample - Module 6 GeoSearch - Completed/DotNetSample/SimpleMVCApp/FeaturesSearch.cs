using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace SimpleSearchMVCApp
{
    public class FeaturesSearch
    {
        private static SearchServiceClient _searchClient;
        private static SearchIndexClient _indexClient;

        public static string errorMessage;

        static FeaturesSearch()
        {
            try
            {
                string searchServiceName = ConfigurationManager.AppSettings["SearchServiceName"];
                string apiKey = ConfigurationManager.AppSettings["SearchServiceApiKey"];

                // Create an HTTP reference to the catalog index
                _searchClient = new SearchServiceClient(searchServiceName, new SearchCredentials(apiKey));
                _indexClient = _searchClient.Indexes.GetClient("geonames");
            }
            catch (Exception e)
            {
                errorMessage = e.Message.ToString();
            }
        }

        public DocumentSearchResult Search(string searchText, string countyFacet)
        {
            // Execute search based on query string
            try
            {
                SearchParameters sp = new SearchParameters()
                {
                    SearchMode = SearchMode.All,
                    // Limit results
                    Select = new List<String>() {"FEATURE_ID", "FEATURE_NAME", "FEATURE_CLASS", "STATE_ALPHA", "COUNTY_NAME", 
                    	"ELEV_IN_M", "ELEV_IN_FT", "MAP_NAME", "LOCATION", "DESCRIPTION", "HISTORY"},
                    // Add count
                    IncludeTotalResultCount = true,
                    // Add search highlights
                    HighlightFields = new List<String>() { "DESCRIPTION" },
                    HighlightPreTag = "<b>",
                    HighlightPostTag = "</b>",
                    // Add facets
                    Facets = new List<String>() { "COUNTY_NAME", "ELEV_IN_FT,interval:50" },
                    ScoringProfile = "usgsScoring"
                };
                // Add filtering
                if (countyFacet != "")
                    sp.Filter = "COUNTY_NAME eq '" + countyFacet + "'";

                return _indexClient.Documents.Search(searchText, sp);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error querying index: {0}\r\n", ex.Message.ToString());
            }
            return null;
        }

        public DocumentSuggestResult Suggest(string searchText, bool fuzzy)
        {
            // Execute search based on query string
            try
            {
                SuggestParameters sp = new SuggestParameters()
                {
                    UseFuzzyMatching = fuzzy,
                    Top = 8
                };

                return _indexClient.Documents.Suggest(searchText, "sg", sp);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error querying index: {0}\r\n", ex.Message.ToString());
            }
            return null;
        }

        public DocumentSearchResult GeoSearch(string lat, string lon, int distance)
        {
            // Execute geo search based on query string
            try
            {
                SearchParameters sp = new SearchParameters()
                {
                    SearchMode = SearchMode.All,
                    Select = new List<String>() { "FEATURE_ID", "FEATURE_NAME", "LOCATION" },
                    Filter = "geo.distance(LOCATION,geography'POINT(" + lon + " " + lat + ")') le " + distance.ToString()
                };

                return _indexClient.Documents.Search("*", sp);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error querying index: {0}\r\n", ex.Message.ToString());
            }
            return null;
        }

    }
}