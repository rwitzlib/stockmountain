using MarketViewer.Application.Handlers.Management.Scanner;
using MarketViewer.Application.Validators;
using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Contracts.Requests.Management.Scanner;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using Xunit;

namespace MarketViewer.Application.UnitTests.Handlers.Scanner;

public class ScannerHandlersUnitTests
{
    private const string UserId = "user-1";
    private const string OtherUserId = "user-2";

    private readonly Mock<IScannerRepository> _repository = new();
    private readonly AuthContext _authContext = new() { UserId = UserId, IsAuthenticated = true };

    private static ScannerDto Scanner(string id = "scanner-1", string userId = UserId) => new()
    {
        Id = id,
        UserId = userId,
        Name = "Oversold bounce",
        EntrySettings = new StrategyEntrySettings { Filters = ["rsi(14) < 30 [1m]"] },
    };

    #region Create

    [Fact]
    public async Task Create_ValidRequest_ReturnsOkWithScanner()
    {
        _repository.Setup(r => r.Create(It.IsAny<ScannerDto>())).ReturnsAsync((ScannerDto s) => s);
        var handler = new ScannerCreateHandler(_authContext, _repository.Object, new ScannerCreateRequestValidator(), NullLogger<ScannerCreateHandler>.Instance);

        var result = await handler.Handle(new ScannerCreateRequest
        {
            Name = "Oversold bounce",
            EntrySettings = new StrategyEntrySettings { Filters = ["rsi(14) < 30 [1m]"] },
        }, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.Equal(UserId, result.Data.UserId);
        Assert.False(string.IsNullOrEmpty(result.Data.Id));
        _repository.Verify(r => r.Create(It.Is<ScannerDto>(s => s.UserId == UserId)), Times.Once);
    }

    [Fact]
    public async Task Create_InvalidExpression_ReturnsBadRequest()
    {
        var handler = new ScannerCreateHandler(_authContext, _repository.Object, new ScannerCreateRequestValidator(), NullLogger<ScannerCreateHandler>.Instance);

        var result = await handler.Handle(new ScannerCreateRequest
        {
            Name = "Broken",
            EntrySettings = new StrategyEntrySettings { Filters = ["rsl(14) < 30"] },
        }, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, result.Status);
        _repository.Verify(r => r.Create(It.IsAny<ScannerDto>()), Times.Never);
    }

    #endregion

    #region Read

    [Fact]
    public async Task Read_OwnScanner_ReturnsOk()
    {
        _repository.Setup(r => r.Get("scanner-1")).ReturnsAsync(Scanner());
        var handler = new ScannerReadHandler(_authContext, _repository.Object, NullLogger<ScannerReadHandler>.Instance);

        var result = await handler.Handle("scanner-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.Equal("scanner-1", result.Data.Id);
    }

    [Fact]
    public async Task Read_OtherUsersScanner_ReturnsNotFound()
    {
        _repository.Setup(r => r.Get("scanner-1")).ReturnsAsync(Scanner(userId: OtherUserId));
        var handler = new ScannerReadHandler(_authContext, _repository.Object, NullLogger<ScannerReadHandler>.Instance);

        var result = await handler.Handle("scanner-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, result.Status);
    }

    [Fact]
    public async Task Read_MissingScanner_ReturnsNotFound()
    {
        _repository.Setup(r => r.Get("nope")).ReturnsAsync((ScannerDto)null!);
        var handler = new ScannerReadHandler(_authContext, _repository.Object, NullLogger<ScannerReadHandler>.Instance);

        var result = await handler.Handle("nope", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, result.Status);
    }

    #endregion

    #region List

    [Fact]
    public async Task List_ReturnsUsersScanners()
    {
        _repository.Setup(r => r.ListByUser(UserId)).ReturnsAsync([Scanner("a"), Scanner("b")]);
        var handler = new ScannerListHandler(_authContext, _repository.Object, NullLogger<ScannerListHandler>.Instance);

        var result = await handler.Handle(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.Equal(2, result.Data.Count());
    }

    #endregion

    #region Update

    [Fact]
    public async Task Update_OwnScanner_ReturnsOk()
    {
        _repository.Setup(r => r.Get("scanner-1")).ReturnsAsync(Scanner());
        _repository.Setup(r => r.Update(It.IsAny<ScannerDto>())).ReturnsAsync((ScannerDto s) => s);
        var handler = new ScannerUpdateHandler(_authContext, _repository.Object, new ScannerUpdateRequestValidator(), NullLogger<ScannerUpdateHandler>.Instance);

        var result = await handler.Handle(new ScannerUpdateRequest
        {
            Id = "scanner-1",
            Name = "Renamed",
            EntrySettings = new StrategyEntrySettings { Filters = ["close > sma(200) [1d]"] },
        }, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.Equal("Renamed", result.Data.Name);
        _repository.Verify(r => r.Update(It.Is<ScannerDto>(s => s.UserId == UserId && s.Id == "scanner-1")), Times.Once);
    }

    [Fact]
    public async Task Update_OtherUsersScanner_ReturnsNotFound()
    {
        _repository.Setup(r => r.Get("scanner-1")).ReturnsAsync(Scanner(userId: OtherUserId));
        var handler = new ScannerUpdateHandler(_authContext, _repository.Object, new ScannerUpdateRequestValidator(), NullLogger<ScannerUpdateHandler>.Instance);

        var result = await handler.Handle(new ScannerUpdateRequest
        {
            Id = "scanner-1",
            Name = "Renamed",
            EntrySettings = new StrategyEntrySettings { Filters = ["close > 1 [1m]"] },
        }, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, result.Status);
        _repository.Verify(r => r.Update(It.IsAny<ScannerDto>()), Times.Never);
    }

    #endregion

    #region Delete

    [Fact]
    public async Task Delete_OwnScanner_ReturnsNoContent()
    {
        _repository.Setup(r => r.Get("scanner-1")).ReturnsAsync(Scanner());
        _repository.Setup(r => r.Delete("scanner-1")).ReturnsAsync(true);
        var handler = new ScannerDeleteHandler(_authContext, _repository.Object, NullLogger<ScannerDeleteHandler>.Instance);

        var result = await handler.Handle("scanner-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, result.Status);
    }

    [Fact]
    public async Task Delete_OtherUsersScanner_ReturnsNotFound()
    {
        _repository.Setup(r => r.Get("scanner-1")).ReturnsAsync(Scanner(userId: OtherUserId));
        var handler = new ScannerDeleteHandler(_authContext, _repository.Object, NullLogger<ScannerDeleteHandler>.Instance);

        var result = await handler.Handle("scanner-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, result.Status);
        _repository.Verify(r => r.Delete(It.IsAny<string>()), Times.Never);
    }

    #endregion
}
