using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;
using System.Data;
using Microsoft.AspNetCore.Http.Extensions;
using ISession = NHibernate.ISession;
using NHibernate;

namespace NHibernateTransactionFilters;

public sealed class TransactionFilter : ActionFilterAttribute, IAsyncExceptionFilter
{
    readonly Stopwatch _sw;
    readonly IsolationLevel _isolationLevel;
    readonly ISession _session;
    readonly ILogger<TransactionFilter> _logger;

    public TransactionFilter(ISession session, ILogger<TransactionFilter> logger, IsolationLevel isolationLevel)
    {
        _session = session;
        _logger = logger;
        _isolationLevel = isolationLevel;
        _sw = new Stopwatch();
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        _logger.LogInformation("正在打开事务：{url}", context.HttpContext.Request.GetDisplayUrl());
        _sw.Start();
        context.HttpContext.Items[typeof(ISession)] = _session;
        context.HttpContext.Items[typeof(ITransaction)] = _session.BeginTransaction(_isolationLevel);

        await next();
    }


    public override async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        await next();

        if (context.HttpContext.Items[typeof(ITransaction)] is ITransaction tx)
        {
            await tx.CommitAsync().ConfigureAwait(false);
            _sw.Stop();
            _logger.LogInformation("已提交事务，{url}，耗时 {elapsedTime} 毫秒", context.HttpContext.Request.GetDisplayUrl(), _sw.ElapsedMilliseconds);
        }
    }


    public async Task OnExceptionAsync(ExceptionContext context)
    {
        _logger.LogWarning("发生错误，事务即将回滚，{url}", context.HttpContext.Request.GetDisplayUrl());
        try
        {
            if (context.HttpContext.Items[typeof(ITransaction)] is ITransaction tx && tx.IsActive)
            {
                await tx.RollbackAsync().ConfigureAwait(false);
                tx.Dispose();
            }
            _sw.Stop();
            _logger.LogWarning("事务已回滚，{url}，耗时 {elapsedTime} 毫秒", context.HttpContext.Request.GetDisplayUrl(), _sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _sw.Stop();
            _logger.LogError(ex, "回滚事务时出错，{url}，耗时 {elapsedTime} 毫秒", context.HttpContext.Request.GetDisplayUrl(), _sw.ElapsedMilliseconds);
        }
    }
}
