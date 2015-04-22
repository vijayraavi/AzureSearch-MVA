//Copyright Microsoft. All rights reserved.
//Licensed under the MIT License at http://mit-license.org/

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using Microsoft.Azure.Search.Models;
using SimpleSearchMVCApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SimpleSearchMVCApp.Controllers
{
    public class HomeController : Controller
    {
        //
        // GET: /Home/
        private FeaturesSearch _featuresSearch = new FeaturesSearch();

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Search(string q = "", string countyFacet = "")
        {
            // If blank search, assume they want to search everything
            if (string.IsNullOrWhiteSpace(q))
                q = "*";

            var response = _featuresSearch.Search(q, countyFacet);
            return new JsonResult
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new USGSResponse() { Results = response.Results, Facets = response.Facets, Count = Convert.ToInt32(response.Count) }
            };
        }

    }
}
