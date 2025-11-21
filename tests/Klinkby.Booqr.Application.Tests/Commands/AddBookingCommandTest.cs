namespace Klinkby.Booqr.Application.Tests.Commands;

public class AddBookingCommandTest
{
    private readonly AddBookingCommand _command;
    private readonly Mock<IBookingRepository> _mockBookingRepository;
    private readonly Mock<ICalendarRepository> _mockCalendarRepository;
    private readonly Mock<IServiceRepository> _mockServiceRepository;
    private readonly Mock<ITransaction> _mockTransaction;
    private readonly Mock<IActivityRecorder> _activityRecorder = new();

    public AddBookingCommandTest()
    {
        _mockBookingRepository = new Mock<IBookingRepository>();
        _mockCalendarRepository = new Mock<ICalendarRepository>();
        _mockServiceRepository = new Mock<IServiceRepository>();
        _mockTransaction = new Mock<ITransaction>();

        _command = new AddBookingCommand(
            _mockBookingRepository.Object,
            _mockCalendarRepository.Object,
            _mockServiceRepository.Object,
            _mockTransaction.Object,
            _activityRecorder.Object,
            NullLogger<AddBookingCommand>.Instance);
    }

    [Fact]
    public async Task GIVEN_NullRequest_WHEN_Execute_THEN_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _command.Execute(null!, CancellationToken.None));
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_ValidRequest_WHEN_Execute_THEN_CallsTransactionBegin(
        AddBookingRequest request,
        Service service,
        CalendarEvent vacancy,
        int newBookingId)
    {
        CalendarEvent updatedVacancy = vacancy with { BookingId = null };

        _mockServiceRepository.Setup(x => x.GetById(request.ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);
        _mockCalendarRepository.Setup(x => x.GetById(request.VacancyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedVacancy);
        _mockBookingRepository.Setup(x => x.Add(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newBookingId);

        await _command.Execute(request);

        _mockTransaction.Verify(x => x.Begin(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_RequestWithNullCustomerId_WHEN_Execute_THEN_SetsCustomerIdToUserId(
        AddBookingRequest request,
        Service service,
        CalendarEvent vacancy,
        int newBookingId)
    {
        AddBookingRequest requestWithNullCustomer = request with { CustomerId = null };
        CalendarEvent updatedVacancy = vacancy with { BookingId = null };

        _mockServiceRepository.Setup(x => x.GetById(request.ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);
        _mockCalendarRepository.Setup(x => x.GetById(request.VacancyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedVacancy);
        _mockBookingRepository.Setup(x => x.Add(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newBookingId);

        await _command.Execute(requestWithNullCustomer);

        _mockBookingRepository.Verify(x => x.Add(
            It.Is<Booking>(b => b.CustomerId == requestWithNullCustomer.AuthenticatedUserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_NonExistentService_WHEN_Execute_THEN_ThrowsArgumentException(
        AddBookingRequest request)
    {
        _mockServiceRepository.Setup(x => x.GetById(0, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Service?)null);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _command.Execute(request));

        Assert.Contains("service", exception.Message, StringComparison.OrdinalIgnoreCase);

        _mockTransaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_NonExistentVacancy_WHEN_Execute_THEN_ThrowsArgumentException(
        AddBookingRequest request,
        Service service)
    {
        _mockServiceRepository.Setup(x => x.GetById(request.ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);
        _mockCalendarRepository.Setup(x => x.GetById(request.VacancyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CalendarEvent?)null);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _command.Execute(request));

        Assert.Contains("vacancy", exception.Message, StringComparison.OrdinalIgnoreCase);

        _mockTransaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_AlreadyBookedVacancyBySameUser_WHEN_Execute_THEN_ReturnsExistingBookingId(
        AddBookingRequest request,
        Service service,
        CalendarEvent vacancy,
        Booking existingBooking,
        int existingBookingId)
    {
        CalendarEvent bookedVacancy = vacancy with { BookingId = existingBookingId };
        Booking matchingBooking = existingBooking with
        {
            CustomerId = request.AuthenticatedUserId,
            ServiceId = request.ServiceId
        };

        _mockServiceRepository.Setup(x => x.GetById(request.ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);
        _mockCalendarRepository.Setup(x => x.GetById(request.VacancyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookedVacancy);
        _mockBookingRepository.Setup(x => x.GetById(existingBookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(matchingBooking);

        var result = await _command.Execute(request);

        Assert.Equal(existingBookingId, result);
        _mockTransaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_VacancyBookedByDifferentUser_WHEN_Execute_THEN_ThrowsInvalidOperationException(
        AddBookingRequest request,
        Service service,
        CalendarEvent vacancy,
        Booking existingBooking,
        int existingBookingId,
        int differentUserId)
    {
        CalendarEvent bookedVacancy = vacancy with { BookingId = existingBookingId };
        Booking differentUserBooking = existingBooking with
        {
            CustomerId = differentUserId,
            ServiceId = request.ServiceId
        };

        _mockServiceRepository.Setup(x => x.GetById(request.ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);
        _mockCalendarRepository.Setup(x => x.GetById(request.VacancyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookedVacancy);
        _mockBookingRepository.Setup(x => x.GetById(existingBookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(differentUserBooking);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _command.Execute(request));

        Assert.Equal("The requested vacancy was already booked.", exception.Message);
        _mockTransaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_ExceptionDuringExecution_WHEN_Execute_THEN_RollsBackTransaction(
        AddBookingRequest request,
        Exception testException)
    {
        _mockServiceRepository.Setup(x => x.GetById(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(testException);

        await Assert.ThrowsAsync<Exception>(() => _command.Execute(request));

        _mockTransaction.Verify(x => x.Begin(It.IsAny<CancellationToken>()), Times.Once);
        _mockTransaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Once);
        _mockTransaction.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [ApplicationAutoData]
    public async Task GIVEN_SuccessfulExecution_WHEN_Execute_THEN_CommitsTransaction(
        AddBookingRequest request,
        Service service,
        CalendarEvent vacancy,
        int newBookingId)
    {
        CalendarEvent availableVacancy = vacancy with { BookingId = null };

        _mockServiceRepository.Setup(x => x.GetById(request.ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);
        _mockCalendarRepository.Setup(x => x.GetById(request.VacancyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(availableVacancy);
        _mockBookingRepository.Setup(x => x.Add(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newBookingId);

        var result = await _command.Execute(request);

        Assert.Equal(newBookingId, result);
        _mockTransaction.Verify(x => x.Begin(It.IsAny<CancellationToken>()), Times.Once);
        _mockTransaction.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Once);
        _mockTransaction.Verify(x => x.Rollback(It.IsAny<CancellationToken>()), Times.Never);
    }

    #region GetCoverage Tests

    [Theory]
    [ApplicationAutoData]
    public void GIVEN_BookingCoversEntireSlot_WHEN_GetCoverage_THEN_ReturnsEntireSlot(
        CalendarEvent vacancy,
        int customerId,
        int vacancyId,
        int serviceId,
        string notes,
        DateTime startTime,
        TimeSpan duration)
    {
        DateTime endTime = startTime.Add(duration);
        vacancy = vacancy with { BookingId = null, StartTime = startTime, EndTime = endTime };
        var request = new AddBookingRequest(
            customerId,
            vacancyId,
            serviceId,
            notes,
            startTime)
        {
            EndTime = endTime
        };

        Covers result = AddBookingCommand.GetCoverage(vacancy, request);

        Assert.Equal(Covers.EntireSlot, result);
    }

    [Theory]
    [ApplicationAutoData]
    public void GIVEN_BookingCoversOnlyBeginning_WHEN_GetCoverage_THEN_ReturnsOnlyBeginning(
        CalendarEvent vacancy,
        AddBookingRequest request,
        Service service
    )
    {
        // Ensure the booking covers only the beginning by:
        // 1. Making booking start at the same time as vacancy
        // 2. Making booking end before vacancy ends (half duration)
        var bookingDuration = TimeSpan.FromTicks(service.Duration.Ticks / 2);
        AddBookingRequest modifiedRequest = request with
        {
            StartTime = vacancy.StartTime
        };
        modifiedRequest = modifiedRequest with
        {
            EndTime = vacancy.StartTime.Add(bookingDuration)
        };

        Covers result = AddBookingCommand.GetCoverage(vacancy, modifiedRequest);

        Assert.Equal(Covers.OnlyBeginning, result);
    }


    [Theory]
    [ApplicationAutoData]
    public void GIVEN_BookingCoversOnlyEnd_WHEN_GetCoverage_THEN_ReturnsOnlyEnd(
        CalendarEvent vacancy,
        AddBookingRequest request,
        Service service)
    {
        // Ensure the booking covers only the end by:
        // 1. Making booking end at the same time as vacancy
        // 2. Making booking start partway through vacancy (half duration from end)
        var bookingDuration = TimeSpan.FromTicks(service.Duration.Ticks / 2);
        DateTime bookingStartTime = vacancy.EndTime.Subtract(bookingDuration);

        AddBookingRequest modifiedRequest = request with
        {
            StartTime = bookingStartTime
        };
        modifiedRequest = modifiedRequest with
        {
            EndTime = vacancy.EndTime
        };

        Covers result = AddBookingCommand.GetCoverage(vacancy, modifiedRequest);

        Assert.Equal(Covers.OnlyEnd, result);
    }

    [Theory]
    [ApplicationAutoData]
    public void GIVEN_BookingInMiddleOfSlot_WHEN_GetCoverage_THEN_ReturnsSomewhereInTheMiddle(
        CalendarEvent vacancy,
        AddBookingRequest request,
        Service service)
    {
        // Ensure the booking is in the middle by:
        // 1. Starting the booking 1/4 into the vacancy
        // 2. Using service duration but ensuring it doesn't extend beyond 3/4 of vacancy
        DateTime bookingStartTime = vacancy.StartTime.Add(TimeSpan.FromTicks(service.Duration.Ticks / 4));
        var maxBookingDuration = TimeSpan.FromTicks(service.Duration.Ticks / 2); // Leave space at both ends
        var actualBookingDuration = TimeSpan.FromTicks(Math.Min(maxBookingDuration.Ticks, service.Duration.Ticks));
        DateTime bookingEndTime = bookingStartTime.Add(actualBookingDuration);

        AddBookingRequest modifiedRequest = request with
        {
            StartTime = bookingStartTime
        };
        modifiedRequest = modifiedRequest with
        {
            EndTime = bookingEndTime
        };

        Covers result = AddBookingCommand.GetCoverage(vacancy, modifiedRequest);

        Assert.Equal(Covers.SomewhereInTheMiddle, result);
    }

    #endregion
}
