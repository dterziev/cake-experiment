using System.Web.Http;

namespace XPlat.WebApiNewCsProj.Controllers
{
    [RoutePrefix("jobs")]
    public class JobController : ApiController
    {
        [HttpGet, Route("")]
        public IHttpActionResult GetJobs()
        {
            return base.Ok(new[] {
                new { id = 1, name = "job 1" },
                new { id = 1, name = "job 1" }
            });
        }
    }
}
