# NHibernateAspNetCoreFilters

`TransactionAttribute` acquires the `ISession` instance from DI and automatically begins a `NHibernate.ITransaction`. 
The transaction would be committed if the action's result executed normally, or rolledback if an exception was thrown.


``` c#

using Microsoft.AspNetCore.Mvc;
using NHibernate;
using NHibernate.Linq;
using ISession = NHibernate.ISession;
using NHibernateAspNetCoreFilters;

namespace Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private readonly ISession _session;
    private readonly ILogger<WeatherForecastController> _logger;

    public WeatherForecastController(ISession session, ILogger<WeatherForecastController> logger)
    {
        _session = session;
        _logger = logger;
    }
    
    [Transaction]
    [HttpGet(Name = "GetWeatherForecast")]
    public Task<List<WeatherForecast>> Get()
    {
        return _session.Query<WeatherForecast>().ToListAsync();
    }
}

```
