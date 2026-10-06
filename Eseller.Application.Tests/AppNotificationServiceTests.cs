using System.Linq.Expressions;
using Eseller.Application.Abstractions;
using Eseller.Application.Services;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Entities.Notifications;
using Eseller.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;

namespace Eseller.Application.Tests;

public class AppNotificationServiceTests
{
    [Fact]
    public async Task NotifyAsync_IsIdempotent_ByEventKey()
    {
        var notifications = new List<Notification>();
        var (ctx, signalR, dateTime) = CreateMocks(notifications, new List<ProductStockAlert>(), new List<Account>());
        var svc = new AppNotificationService(ctx.Object, dateTime.Object, signalR.Object);
        var userId = Guid.NewGuid();

        await svc.NotifyAsync(
            userId, RoleType.Shopkeeper, NotificationType.NewOrder,
            "New Order", "Order created",
            NotificationEntityType.Order, Guid.NewGuid(), "/orders",
            "NewOrder:abc:Seller:1", CancellationToken.None);

        await svc.NotifyAsync(
            userId, RoleType.Shopkeeper, NotificationType.NewOrder,
            "New Order", "duplicate",
            NotificationEntityType.Order, Guid.NewGuid(), "/orders",
            "NewOrder:abc:Seller:1", CancellationToken.None);

        Assert.Single(notifications);
    }

    [Fact]
    public async Task ProductOutOfStock_NotifiesOnce_UntilResolved()
    {
        var notifications = new List<Notification>();
        var alerts = new List<ProductStockAlert>();
        var adminId = Guid.NewGuid();
        var accounts = new List<Account>
        {
            new()
            {
                Id = adminId,
                Username = "admin",
                Email = "admin@test.com",
                PasswordHash = "x",
                RoleType = RoleType.SuperAdmin
            }
        };

        var (ctx, signalR, dateTime) = CreateMocks(notifications, alerts, accounts);
        var svc = new AppNotificationService(ctx.Object, dateTime.Object, signalR.Object);
        var productId = Guid.NewGuid();

        await svc.NotifyProductOutOfStockOnceAsync(productId, "Widget", CancellationToken.None);
        await svc.NotifyProductOutOfStockOnceAsync(productId, "Widget", CancellationToken.None);

        Assert.Single(alerts.Where(a => a.ResolvedAt == null));
        Assert.Single(notifications.Where(n => n.Type == NotificationType.ProductOutOfStock));

        await svc.ResolveProductStockAlertAsync(productId, CancellationToken.None);
        Assert.All(alerts, a => Assert.NotNull(a.ResolvedAt));

        await svc.NotifyProductOutOfStockOnceAsync(productId, "Widget", CancellationToken.None);

        Assert.Equal(2, alerts.Count);
        Assert.Single(alerts.Where(a => a.ResolvedAt == null));
        Assert.Equal(2, notifications.Count(n => n.Type == NotificationType.ProductOutOfStock));
    }

    [Fact]
    public async Task Notifications_AreScoped_ToRecipientUserId()
    {
        var notifications = new List<Notification>();
        var (ctx, signalR, dateTime) = CreateMocks(notifications, new List<ProductStockAlert>(), new List<Account>());
        var svc = new AppNotificationService(ctx.Object, dateTime.Object, signalR.Object);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        await svc.NotifyAsync(a, RoleType.SuperAdmin, NotificationType.NewSeller,
            "Seller", "msg", NotificationEntityType.Seller, Guid.NewGuid(), "/sellers",
            "NewSeller:1:Admin:A", CancellationToken.None);
        await svc.NotifyAsync(b, RoleType.Shopkeeper, NotificationType.NewOrder,
            "Order", "msg", NotificationEntityType.Order, Guid.NewGuid(), "/orders",
            "NewOrder:1:Seller:B", CancellationToken.None);

        Assert.Equal(1, notifications.Count(n => n.RecipientUserId == a));
        Assert.Equal(1, notifications.Count(n => n.RecipientUserId == b));
    }

    private static (
        Mock<IApplicationDbContext> Ctx,
        Mock<ISignalRNotificationService> SignalR,
        Mock<IDateTime> DateTime)
        CreateMocks(
            List<Notification> notifications,
            List<ProductStockAlert> alerts,
            List<Account> accounts)
    {
        var ctx = new Mock<IApplicationDbContext>();
        ctx.Setup(c => c.Notifications).Returns(CreateDbSet(notifications));
        ctx.Setup(c => c.ProductStockAlerts).Returns(CreateDbSet(alerts));
        ctx.Setup(c => c.Accounts).Returns(CreateDbSet(accounts));
        ctx.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var dateTime = new Mock<IDateTime>();
        dateTime.Setup(d => d.UtcNow).Returns(new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc));

        var signalR = new Mock<ISignalRNotificationService>();
        signalR.Setup(s => s.SendNotificationToAccountAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        signalR.Setup(s => s.SendUnreadCountChangedAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return (ctx, signalR, dateTime);
    }

    private static DbSet<T> CreateDbSet<T>(List<T> data) where T : class
    {
        var queryable = data.AsQueryable();
        var mockSet = new Mock<DbSet<T>>();
        mockSet.As<IAsyncEnumerable<T>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken _) => new TestAsyncEnumerator<T>(queryable.GetEnumerator()));
        mockSet.As<IQueryable<T>>().Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<T>(queryable.Provider));
        mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
        mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => queryable.GetEnumerator());
        mockSet.Setup(d => d.Add(It.IsAny<T>())).Callback<T>(data.Add);
        mockSet.Setup(d => d.Remove(It.IsAny<T>())).Callback<T>(e => data.Remove(e));
        return mockSet.Object;
    }
}

internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;
    public TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;
    public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<TEntity>(expression);
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new TestAsyncEnumerable<TElement>(expression);
    public object? Execute(Expression expression) => _inner.Execute(expression);
    public TResult Execute<TResult>(Expression expression) => _inner.Execute<TResult>(expression);
    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        var resultType = typeof(TResult).GetGenericArguments()[0];
        var executeMethod = typeof(IQueryProvider)
            .GetMethod(nameof(IQueryProvider.Execute), 1, [typeof(Expression)])!
            .MakeGenericMethod(resultType);
        var result = executeMethod.Invoke(_inner, [expression]);
        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(resultType)
            .Invoke(null, [result])!;
    }
}

internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
    public TestAsyncEnumerable(Expression expression) : base(expression) { }
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
}

internal class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
{
    public T Current => inner.Current;
    public ValueTask DisposeAsync()
    {
        inner.Dispose();
        return ValueTask.CompletedTask;
    }
    public ValueTask<bool> MoveNextAsync() => new(inner.MoveNext());
}
