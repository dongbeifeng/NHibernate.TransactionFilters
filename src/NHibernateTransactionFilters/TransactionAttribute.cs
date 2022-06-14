using Microsoft.AspNetCore.Mvc;
using System.Data;

namespace NHibernateTransactionFilters;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class TransactionAttribute : TypeFilterAttribute
{
    public TransactionAttribute(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        : base(typeof(TransactionFilter))
    {
        this.Arguments = new object[] { isolationLevel, };
        this.IsReusable = false;
    }

}
