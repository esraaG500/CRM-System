using Asp.Versioning;
using Crm.Application.Common.Messaging;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

[ApiController]
[ApiVersion(1.0)]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected IDispatcher Dispatcher => HttpContext.RequestServices.GetRequiredService<IDispatcher>();
}
