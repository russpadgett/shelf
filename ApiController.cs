using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;

namespace WebServices.RestApis
{

    [Authorize]
    [RoutePrefix("Domain/SubDomain")]
    public class Api : BaseApiController
    {
        [Route("{id}")]
        [HttpPost]
        [ResponseType(typeof(IApiResponse<List<Dto>>))]
        public async Task<IHttpActionResult> GetData(int uid, int page, int pageSize, QueryOptions queryOptions)
        {
            try
            {
                var result = (await DataManager.GetData(page, pageSize, queryOptions)).MapDown();                

                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result.Message);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error getting Data: {ex.Message}");
            }
        }
    }
}