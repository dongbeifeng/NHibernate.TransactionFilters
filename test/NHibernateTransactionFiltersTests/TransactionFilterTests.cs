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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using NHibernate;
using NHibernateTransactionFilters;
using NSubstitute;
using System.Data;
using Xunit;
using static NSubstitute.Substitute;

namespace NHibernateTransactionFiltersTests;

public class TransactionFilterTests
{
    private (ActionExecutingContext actionExecutingContext, ActionExecutedContext actionExecutedContext) createExecutionContexts(ActionContext actionContext)
    {
        var actionExecutingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            For<Controller>()
            );

        var actionExecutedContext = new ActionExecutedContext(
            actionContext,
            new List<IFilterMetadata>(),
            actionExecutingContext.Controller
            );

        return (actionExecutingContext, actionExecutedContext);
    }

    private (ResultExecutingContext actionExecutingContext, ResultExecutedContext actionExecutedContext) createResultContexts(ActionContext actionContext)
    {
        var resultExecutingContext = new ResultExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            For<IActionResult>(),
            For<Controller>()
            );

        var resultExecutedContext = new ResultExecutedContext(
            actionContext,
            new List<IFilterMetadata>(),
            For<IActionResult>(),
            For<Controller>()
            );
        return (resultExecutingContext, resultExecutedContext);
    }

    private ILogger<TransactionFilter> CreateLogger()
    {
        LoggerFactory loggerFactory = new LoggerFactory();            
        var logger = loggerFactory.CreateLogger<TransactionFilter>();
        return logger;
    }

    [Fact()]
    public async Task OnActionExecutionAsyncTest()
    {
        // Arrange
        var actionContext = new ActionContext() 
        {
            HttpContext = new DefaultHttpContext(), 
            RouteData = new RouteData(), 
            ActionDescriptor = new ActionDescriptor() 
        };

        var (actionExecutingContext, actionExecutedContext) = createExecutionContexts(actionContext);

        // Act
        var session = For<NHibernate.ISession>();
        TransactionFilter filter = new TransactionFilter(session, CreateLogger(), IsolationLevel.RepeatableRead);
        await filter.OnActionExecutionAsync(actionExecutingContext, () => Task.FromResult(actionExecutedContext));

        // Assert
        Assert.Same(session, actionContext.HttpContext.Items[typeof(NHibernate.ISession)]);
        var tx = session.Received().BeginTransaction(IsolationLevel.RepeatableRead);
        Assert.Same(tx, actionContext.HttpContext.Items[typeof(ITransaction)]);

    }


    [Fact()]
    public async Task OnResultExecutionAsyncTest()
    {
        // Arrange
        var actionContext = new ActionContext()
        {
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData(),
            ActionDescriptor = new ActionDescriptor()
        };
        var (actionExecutingContext, actionExecutedContext) = createExecutionContexts(actionContext);
        var (resultExecutingContext, resultExecutedContext) = createResultContexts(actionContext);

        // Act
        var session = For<NHibernate.ISession>();
        TransactionFilter filter = new TransactionFilter(session, CreateLogger(), IsolationLevel.RepeatableRead);
        await filter.OnActionExecutionAsync(actionExecutingContext, () => Task.FromResult(actionExecutedContext));
        await filter.OnResultExecutionAsync(resultExecutingContext, () => Task.FromResult(resultExecutedContext));

        // Assert
        var tx = session.Received().BeginTransaction(IsolationLevel.RepeatableRead);
        await tx.Received().CommitAsync();
    }


    [Fact()]
    public async Task OnExceptionAsyncTest()
    {
        // Arrange
        var actionContext = new ActionContext()
        {
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData(),
            ActionDescriptor = new ActionDescriptor()
        };
        var (actionExecutingContext, actionExecutedContext) = createExecutionContexts(actionContext);

        var exceptionContext = new ExceptionContext(
            actionContext,
            new List<IFilterMetadata>()
            );

        // Act
        var session = For<NHibernate.ISession>();
        TransactionFilter filter = new TransactionFilter(session, CreateLogger(), IsolationLevel.RepeatableRead);
        await filter.OnActionExecutionAsync(actionExecutingContext, () => Task.FromResult(actionExecutedContext));
        var tx = session.Received().BeginTransaction(IsolationLevel.RepeatableRead);
        tx.IsActive.Returns(true);

        await filter.OnExceptionAsync(exceptionContext);

        // Assert
        await tx.Received().RollbackAsync();
    }
}