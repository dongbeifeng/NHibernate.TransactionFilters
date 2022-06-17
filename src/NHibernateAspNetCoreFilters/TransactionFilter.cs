// Copyright 2022 王建军
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using NHibernate;
using System.Data;
using System.Diagnostics;

namespace NHibernateAspNetCoreFilters;

/// <summary>
/// 为 Action 打开 <see cref="ITransaction"/> 的筛选器。如果没有抛出异常，则在 Result 执行后提交事务，否则回滚事务。
/// </summary>
internal sealed class TransactionFilter : ActionFilterAttribute, IAsyncExceptionFilter
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

    /// <summary>
    /// 在 Action 执行之前打开事务。
    /// </summary>
    /// <param name="context"></param>
    /// <param name="next"></param>
    /// <returns></returns>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        _logger.LogInformation("正在开始事务，隔离级别是 {isolationLevel}", _isolationLevel);
        _sw.Start();
        context.HttpContext.Items[typeof(ISession)] = _session;
        context.HttpContext.Items[typeof(ITransaction)] = _session.BeginTransaction(_isolationLevel);


        await next();
    }

    /// <summary>
    /// 在 Result 执行之后提交事务。
    /// </summary>
    /// <param name="context"></param>
    /// <param name="next"></param>
    /// <returns></returns>
    public override async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        await next();
        if (context.HttpContext.Items[typeof(ITransaction)] is ITransaction tx)
        {
            await tx.CommitAsync().ConfigureAwait(false);
            _sw.Stop();
            _logger.LogInformation("已提交事务，共耗时 {milliseconds} 毫秒", _sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// 在出现错误时回滚事务。
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        _logger.LogWarning("发生错误，即将回滚事务");
        try
        {
            if (context.HttpContext.Items[typeof(ITransaction)] is ITransaction tx && tx.IsActive)
            {
                await tx.RollbackAsync().ConfigureAwait(false);
                tx.Dispose();
                _sw.Stop();
                _logger.LogInformation("已回滚事务，共耗时 {milliseconds} 毫秒", _sw.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            _sw.Stop();
            _logger.LogError(ex, "回滚事务时出错，共耗时 {milliseconds} 毫秒", _sw.ElapsedMilliseconds);
        }
    }
}
