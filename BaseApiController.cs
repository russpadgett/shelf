using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Results;


namespace WebServices.RestApis
{
    public abstract class BaseApiController : ApiController
    {
        protected ILogger Log;
      
        protected BaseApiController()
        {
           Log = new Logger();
        }
        protected BaseApiController(ILogger logger)
        {
            Log = logger;
        }
        protected NotFoundNegotiatedContentResult NotFound(string message)
        {
            return new NotFoundNegotiatedContentResult(message, this);
        }

        protected InternalServerErrorNegotiatedContentResult InternalServerError(string message)
        {
            return new InternalServerErrorNegotiatedContentResult(message, this);
        }

        protected UnauthorizedNegotiatedContentResult Unauthorized(string message)
        {
            return new UnauthorizedNegotiatedContentResult(message, this);
        }

        protected CreatedNegotiatedContentResult<T> Created<T>(T content)
        {
            return new CreatedNegotiatedContentResult<T>(content, this);
        }

        protected int GetUserId()
        {
            if (!(this.RequestContext?.Principal?.Identity is ClaimsIdentity identity))
            {
                throw new InvalidOperationException("Cannot get User Id from Token - No identity defined");
            }

            return identity.Claims.Where(c => c.Type == CustomClaimTypes.USER_ID).Select(c => c.Value.ConvertToIntOrNull()).FirstOrDefault() ?? throw new InvalidOperationException("Cannot get User Id from Token - User Id claim not found or invalid");
        }

        /// <summary>
        /// This method sets up a <see cref="PushStreamContent"/> which pushes down a keep-alive every 30 seconds so that the Azure Load Balancer does not close the connection after 4 minutes
        /// </summary>
        /// <param name="request">The HTTP request</param>
        /// <param name="process">The asynchronous process to wait on</param>
        /// <returns>An <see cref="HttpResponseMessage"/> with the <see cref="PushStreamContent"/> as the content</returns>
        /// <remarks>Only use this if the response of the message does not matter, as it will be sending text down the response stream until it finishes</remarks>
        protected static HttpResponseMessage ResponseWithTimeoutPrevention(HttpRequestMessage request, Task process)
        {
            //var startTime = DateTime.UtcNow;
            HttpResponseMessage response = request.CreateResponse();

            response.Content = new PushStreamContent(async (outputStream, httpContentExtensions, transportContext) =>
            {
                using (var streamWriter = new StreamWriter(outputStream))
                {
                    await streamWriter.WriteLineAsync("Started");
#pragma warning disable 4014
                    // Don't need to wait for this, just let it flush in the background
                    streamWriter.FlushAsync().ContinueWith(async (antecedent) => await outputStream.FlushAsync());
#pragma warning restore 4014

                    while ((await Task.WhenAny(process, Task.Delay(TimeSpan.FromSeconds(30)))) != process)
                    {
                        await streamWriter.WriteLineAsync($"{DateTime.UtcNow} - Working...");
#pragma warning disable 4014
                        // Don't need to wait for this, just let it flush in the background
                        streamWriter.FlushAsync().ContinueWith(async (antecedent) => await outputStream.FlushAsync());
#pragma warning restore 4014
                    }

                    if (!process.IsCompleted)
                    {
                        throw new Exception("Process did not complete");
                    }

                    if (process.IsFaulted)
                    {
                        throw new Exception("Process failed", process.Exception);
                    }

                    await streamWriter.WriteLineAsync("Success");
                }

                outputStream.Close();
            });

            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

            return response;
        }
    }

    public class UnauthorizedNegotiatedContentResult : NegotiatedContentResult<string>
    {
        public UnauthorizedNegotiatedContentResult(string message, ApiController controller)
            : base(HttpStatusCode.Unauthorized, message, controller) { }
    }

    public class NotFoundNegotiatedContentResult : NegotiatedContentResult<string>
    {
        public NotFoundNegotiatedContentResult(string message, ApiController controller)
            : base(HttpStatusCode.NotFound, message, controller) { }
    }

    public class InternalServerErrorNegotiatedContentResult : NegotiatedContentResult<string>
    {
        public InternalServerErrorNegotiatedContentResult(string message, ApiController controller)
            : base(HttpStatusCode.InternalServerError, message, controller) { }
    }

    public class CreatedNegotiatedContentResult<T> : NegotiatedContentResult<T>
    {
        private readonly T _content;

        public CreatedNegotiatedContentResult(T content, ApiController controller) : base(HttpStatusCode.Created, content, controller)
        {
            _content = content;
        }

        public override Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            if (_content == null)
            {
                return base.ExecuteAsync(cancellationToken);
            }

            var message = new HttpResponseMessage
            {
                Content = new ObjectContent<T>(_content, new JsonMediaTypeFormatter()),
                StatusCode = HttpStatusCode.Created
            };

            var idProp = typeof(T).GetProperties().FirstOrDefault(prop => Attribute.IsDefined(prop, typeof(UniqueIdAttribute)));

            if (idProp == null)
            {
                return Task.FromResult(message);
            }

            var idValue = idProp.GetValue(_content).ToString();
            message.Headers.Add("location", $"{Request.RequestUri}/{idValue}");

            return Task.FromResult(message);
        }
    }
}
