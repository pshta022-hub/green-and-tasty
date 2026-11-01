using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GroupReservation;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.GuestSessions;
using APIHandler.Services.Locations;
using APIHandler.Services.Orders;
using AutoFixture.Xunit2;
using Moq;

namespace APIHandler.Tests.Services;

public class GroupReservationServiceTests
{
    private readonly Mock<IGroupReservationRepository> _groupReservationRepositoryMock = new();
    private readonly Mock<ITableRepository> _tableRepositoryMock = new();
    private readonly Mock<IReservationRepository> _reservationRepositoryMock = new();
    private readonly Mock<IGuestSessionRepository> _guestSessionRepositoryMock = new();
    private readonly Mock<IUserService> _userServiceMock = new();
    private readonly Mock<ILocationService> _locationServiceMock = new();
    private readonly Mock<IGuestSessionService> _guestSessionServiceMock = new();
    private readonly Mock<IOrderService> _orderServiceMock = new();

    private readonly GroupReservationService _service;

    public GroupReservationServiceTests()
    {
        _service = new GroupReservationService(
            _groupReservationRepositoryMock.Object,
            _tableRepositoryMock.Object,
            _reservationRepositoryMock.Object,
            _guestSessionRepositoryMock.Object,
            _userServiceMock.Object,
            _locationServiceMock.Object,
            _guestSessionServiceMock.Object,
            _orderServiceMock.Object);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_ValidInput_ReturnsGroupReservationInfo(
        GroupReservation groupReservation,
        Table table,
        Reservation reservation,
        Location location,
        string expectedUserName)
    {
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;
        reservation.TableId = table.Id;
        table.LocationId = location.Id;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _guestSessionRepositoryMock
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(0);

        _tableRepositoryMock
            .Setup(r => r.GetTableAsync(table.Id))
            .ReturnsAsync(table);

        _locationServiceMock
            .Setup(s => s.GetLocationByIdAsync(table.LocationId))
            .ReturnsAsync(location);

        _userServiceMock
            .Setup(s => s.GetUserName(reservation.CustomerId!))
            .ReturnsAsync(expectedUserName);

        var result = await _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken);

        Assert.Equal(groupReservation.GroupReservationId, result.GroupReservationId);
        Assert.Equal(table.LocationId, result.LocationId);
        Assert.Equal(reservation.GuestCount, result.MaxGuestCount);
        Assert.Equal(reservation.StartTime, result.StartTime);
        Assert.Equal(reservation.EndTime, result.EndTime);
        Assert.Equal(table.TableNumber, result.TableNumber);
        Assert.Equal("Green & Tasty", result.RestaurantName);
        Assert.Equal(location.Address, result.LocationAddress);
        Assert.Equal(expectedUserName, result.InviterName);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_InvalidToken_ThrowsNotFound(string token)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(token))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _service.GetGroupReservationByTokenAsync(token));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Closed)]
    [InlineAutoData(GroupReservationStatus.Submitted)]
    public async Task GetGroupReservationByTokenAsync_GroupReservationIsNotOpen_ThrowsException(
        GroupReservationStatus status,
        GroupReservation groupReservation)
    {
        groupReservation.Status = status;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<GroupReservationExpiredException>(() => _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_WhenGuestCountIsMax_ThrowsGuestLimitExceeded(GroupReservation groupReservation, Reservation reservation)
    {
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.ReservationId = reservation.Id;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _guestSessionRepositoryMock
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(reservation.GuestCount);

        await Assert.ThrowsAsync<GuestLimitExceededException>(() => _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken));
    }

    [Theory]
    [AutoData]
    public async Task CloseGroupReservationAsync_ValidInput_UpdatesGroupReservationStatus(GroupReservation groupReservation)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(groupReservation.ReservationId))
            .ReturnsAsync(groupReservation);

        GroupReservation? savedGroupReservation = null;
        _groupReservationRepositoryMock
            .Setup(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()))
            .Callback<GroupReservation>(r => savedGroupReservation = r);

        await _service.CloseGroupReservationAsync(groupReservation.ReservationId);

        Assert.NotNull(savedGroupReservation);
        Assert.Equal(GroupReservationStatus.Closed, savedGroupReservation.Status);
    }

    [Theory]
    [AutoData]
    public async Task CloseGroupReservationAsync_GroupReservationNotFound_DoesNotThrowException(string reservationId)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservationId))
            .ReturnsAsync((GroupReservation?)null);

        await _service.CloseGroupReservationAsync(reservationId);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_ValidInput_ReturnsToken(Reservation reservation, GroupReservation groupReservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(groupReservation);

        GetGroupInviteTokenResponse result = await _service.GetGroupReservationInviteTokenAsync(reservation.Id);

        Assert.Equal(groupReservation.ReservationToken, result.Token);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_InvalidReservationId_ThrowsNotFound(string reservationId)
    {
        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() => _service.GetGroupReservationInviteTokenAsync(reservationId));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_OneGuest_ThrowsValidationException(Reservation reservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 1;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [InlineAutoData(ReservationStatus.Completed)]
    [InlineAutoData(ReservationStatus.Cancelled)]
    [InlineAutoData(ReservationStatus.InProgress)]
    public async Task GetGroupReservationInviteTokenAsync_StatusIsNotReserved_ThrowsValidationException(ReservationStatus reservationStatus, Reservation reservation)
    {
        reservation.Status = reservationStatus;
        reservation.GuestCount = 1;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    public async Task GetGroupReservationInviteTokenAsync_GroupStatusIsNotOpen_ThrowsException(GroupReservationStatus status, Type type, Reservation reservation, GroupReservation groupReservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = status;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync(type, () => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [AutoData]
    public async Task CreateGroupReservationAsync_CustomerReservation_CreatesOwnerSession(
        Reservation reservation,
        string customerId)
    {
        reservation.CustomerId = customerId;
        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);
        _guestSessionServiceMock
            .Setup(s => s.CreateGroupOwnerSessionAsync(customerId, It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var result = await _service.CreateGroupReservationAsync(reservation.Id);

        _guestSessionServiceMock.Verify(
            s => s.CreateGroupOwnerSessionAsync(customerId, result.GroupReservationId),
            Times.Once);
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupReservationAsync_Owner_ConsolidatesOnlySubmittedSessions(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId,
        string guestName)
    {
        groupReservation.ReservationId = reservation.Id;
        reservation.CustomerId = userId;

        var sessions = new List<GuestSession>
        {
            new()
            {
                Id = "submitted-1",
                GroupReservationId = groupReservation.GroupReservationId,
                GuestName = guestName,
                Dishes =
                [
                    new OrderItem { DishId = "dish-a", Quantity = 2 },
                    new OrderItem { DishId = "dish-b", Quantity = 1 }
                ],
                IsSubmitted = true
            },
            new()
            {
                Id = "submitted-2",
                GroupReservationId = groupReservation.GroupReservationId,
                GuestName = "Another guest",
                Dishes = [new OrderItem { DishId = "dish-a", Quantity = 3 }],
                IsSubmitted = true
            },
            new()
            {
                Id = "not-submitted",
                GroupReservationId = groupReservation.GroupReservationId,
                GuestName = "Not submitted",
                Dishes = [new OrderItem { DishId = "dish-a", Quantity = 100 }],
                IsSubmitted = false
            }
        };
        var consolidatedOrder = new List<OrderItem>
        {
            new() { DishId = "dish-a", Quantity = 5 },
            new() { DishId = "dish-b", Quantity = 1 }
        };

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);
        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);
        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(sessions);
        _orderServiceMock
            .Setup(s => s.AddGroupReservationDishesAsync(reservation.Id, It.IsAny<List<OrderItem>>()))
            .ReturnsAsync(consolidatedOrder);

        var result = await _service.SubmitGroupReservationAsync(
            userId,
            groupReservation.GroupReservationId,
            reservation.Id);

        Assert.Equal(GroupReservationStatus.Submitted, groupReservation.Status);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.DishId == "dish-a" && item.Quantity == 5);
        Assert.Contains(result, item => item.DishId == "dish-b" && item.Quantity == 1);
        _groupReservationRepositoryMock.Verify(r => r.SaveGroupReservationAsync(groupReservation), Times.Once);
        _orderServiceMock.Verify(
            s => s.AddGroupReservationDishesAsync(
                reservation.Id,
                It.Is<List<OrderItem>>(items => items.Count == 2 &&
                    items.Any(item => item.DishId == "dish-a" && item.Quantity == 5) &&
                    items.Any(item => item.DishId == "dish-b" && item.Quantity == 1))),
            Times.Once);
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupReservationAsync_NoSubmittedSessions_ThrowsValidationException(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        groupReservation.ReservationId = reservation.Id;
        reservation.CustomerId = userId;
        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);
        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);
        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync([]);

        await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitGroupReservationAsync(
            userId,
            groupReservation.GroupReservationId,
            reservation.Id));
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupReservationAsync_SubmittedSessionsWithoutDishes_ThrowsValidationException(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId)
    {
        groupReservation.ReservationId = reservation.Id;
        reservation.CustomerId = userId;
        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);
        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);
        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(
            [
                new GuestSession
                {
                    Id = "empty-submitted",
                    GroupReservationId = groupReservation.GroupReservationId,
                    GuestName = "Guest",
                    Dishes = [],
                    IsSubmitted = true
                }
            ]);

        await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitGroupReservationAsync(
            userId,
            groupReservation.GroupReservationId,
            reservation.Id));
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupReservationAsync_NonOwner_ThrowsActionForbiddenException(
        GroupReservation groupReservation,
        Reservation reservation,
        string userId,
        string ownerId)
    {
        groupReservation.ReservationId = reservation.Id;
        reservation.CustomerId = ownerId;
        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);
        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ActionForbiddenException>(() => _service.SubmitGroupReservationAsync(
            userId,
            groupReservation.GroupReservationId,
            reservation.Id));
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupReservationAsync_ReservationMissing_ThrowsReservationNotFoundException(
        GroupReservation groupReservation,
        string reservationId,
        string userId)
    {
        groupReservation.ReservationId = reservationId;
        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);
        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() => _service.SubmitGroupReservationAsync(
            userId,
            groupReservation.GroupReservationId,
            reservationId));
    }
}
