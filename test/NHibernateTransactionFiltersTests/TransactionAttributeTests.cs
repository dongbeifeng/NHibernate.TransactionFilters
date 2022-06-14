using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using NHibernateTransactionFilters;
using static NSubstitute.Substitute;
using Xunit;
using Microsoft.Extensions.Logging;
using System.Data;
using NSubstitute;
using NHibernate;

namespace NHibernateTransactionFiltersTests
{
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
            var logger = For<ILogger<TransactionFilter>>();
            TransactionFilter filter = new TransactionFilter(session, logger, IsolationLevel.RepeatableRead);
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
            var logger = For<ILogger<TransactionFilter>>();
            TransactionFilter filter = new TransactionFilter(session, logger, IsolationLevel.RepeatableRead);
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
            var logger = For<ILogger<TransactionFilter>>();
            TransactionFilter filter = new TransactionFilter(session, logger, IsolationLevel.RepeatableRead);
            await filter.OnActionExecutionAsync(actionExecutingContext, () => Task.FromResult(actionExecutedContext));
            var tx = session.Received().BeginTransaction(IsolationLevel.RepeatableRead);
            tx.IsActive.Returns(true);

            await filter.OnExceptionAsync(exceptionContext);

            // Assert
            await tx.Received().RollbackAsync();
        }
    }
}