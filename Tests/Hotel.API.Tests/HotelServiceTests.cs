using Hotel.API.Data;
using Hotel.API.Entities;
using Hotel.API.Enums;
using Hotel.API.Repositories;
using Hotel.API.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace Hotel.API.Tests;

/*
 * These tests cover HotelService — the class that handles all hotel CRUD operations.
 * The two most important things being tested here are ownership enforcement (a manager
 * can only edit/delete their own hotels) and the EF tracking conflict workaround in UpdateHotelAsync.
 *
 * We use two different approaches depending on what the method does:
 * - Methods that use _context directly (GetAll, GetById, Update, GetByCity) get tested against
 *   an EF Core InMemory database so we can test the actual LINQ queries.
 * - Methods that go through the generic repository (Create, Delete) get tested with a Moq mock
 *   so we can verify the repository methods were called correctly.
 */
[TestFixture]
public class HotelServiceTests
{
    private HotelDbContext _context;
    private Mock<IRepository<HotelEntity>> _repoMock;
    private Mock<ICloudinaryService> _cloudinaryMock;
    private HotelService _service;

    /*
     * [SetUp] runs before every test. We create a fresh InMemory database with a unique name
     * for each test — this is critical. If all tests shared the same database name, data inserted
     * in one test would bleed into the next and cause false failures or false passes.
     */
    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<HotelDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context        = new HotelDbContext(options);
        _repoMock       = new Mock<IRepository<HotelEntity>>();
        _cloudinaryMock = new Mock<ICloudinaryService>();
        _service        = new HotelService(_repoMock.Object, _context, _cloudinaryMock.Object);
    }

    // [TearDown] runs after every test — we dispose the DbContext to release the InMemory database resources.
    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    // ── GetAllHotelsAsync ─────────────────────────────────────────────────────

    // Baseline test — a fresh database should return nothing, not throw an exception.
    [Test]
    public async Task GetAllHotelsAsync_EmptyDb_ReturnsEmptyList()
    {
        var result = await _service.GetAllHotelsAsync();
        Assert.That(result, Is.Empty);
    }

    // Verifies that all hotels are returned regardless of which manager owns them.
    [Test]
    public async Task GetAllHotelsAsync_WithHotels_ReturnsAll()
    {
        _context.Hotels.AddRange(BuildHotel("manager-1"), BuildHotel("manager-1"), BuildHotel("manager-2"));
        await _context.SaveChangesAsync();

        var result = await _service.GetAllHotelsAsync();

        Assert.That(result.Count(), Is.EqualTo(3));
    }

    // ── GetHotelByIdAsync ─────────────────────────────────────────────────────

    [Test]
    public async Task GetHotelByIdAsync_ExistingId_ReturnsHotel()
    {
        var hotel = BuildHotel("manager-1");
        _context.Hotels.Add(hotel);
        await _context.SaveChangesAsync();

        var result = await _service.GetHotelByIdAsync(hotel.Id);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(hotel.Id));
    }

    // A non-existent ID should return null, not throw a KeyNotFoundException or similar.
    [Test]
    public async Task GetHotelByIdAsync_NonExistentId_ReturnsNull()
    {
        var result = await _service.GetHotelByIdAsync(Guid.NewGuid());
        Assert.That(result, Is.Null);
    }

    // ── GetHotelsByManagerAsync ───────────────────────────────────────────────

    /*
     * This is the query that powers the partner dashboard. We seed hotels for two different managers
     * and verify that only the correct manager's hotels come back — the WHERE clause must be working.
     */
    [Test]
    public async Task GetHotelsByManagerAsync_ReturnsOnlyManagersHotels()
    {
        _context.Hotels.Add(BuildHotel("manager-A"));
        _context.Hotels.Add(BuildHotel("manager-A"));
        _context.Hotels.Add(BuildHotel("manager-B"));
        await _context.SaveChangesAsync();

        var result = await _service.GetHotelsByManagerAsync("manager-A");

        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result.All(h => h.ManagerId == "manager-A"), Is.True);
    }

    // An unknown manager ID should return an empty list, not null or an exception.
    [Test]
    public async Task GetHotelsByManagerAsync_UnknownManager_ReturnsEmpty()
    {
        _context.Hotels.Add(BuildHotel("manager-A"));
        await _context.SaveChangesAsync();

        var result = await _service.GetHotelsByManagerAsync("manager-X");

        Assert.That(result, Is.Empty);
    }

    // ── CreateHotelAsync ──────────────────────────────────────────────────────

    /*
     * This is a security-critical test. The ManagerId must be stamped from the JWT (passed as a
     * parameter), not from whatever the client sent in the request body. We pass an empty ManagerId
     * in the hotel object to simulate a client trying to bypass ownership, and verify the service
     * overwrites it with the correct value.
     */
    [Test]
    public async Task CreateHotelAsync_StampsManagerId()
    {
        var hotel = BuildHotel(string.Empty);
        _repoMock.Setup(r => r.AddAsync(It.IsAny<HotelEntity>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await _service.CreateHotelAsync(hotel, "manager-99");

        Assert.That(result.ManagerId, Is.EqualTo("manager-99"));
    }

    // The service must generate a new GUID for the hotel ID — the client shouldn't control this.
    [Test]
    public async Task CreateHotelAsync_AssignsNewGuid()
    {
        var hotel = BuildHotel("manager-1");
        hotel.Id  = Guid.Empty;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<HotelEntity>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await _service.CreateHotelAsync(hotel, "manager-1");

        Assert.That(result.Id, Is.Not.EqualTo(Guid.Empty));
    }

    // Verifies the service actually persists the hotel — both AddAsync and SaveChangesAsync must be called.
    [Test]
    public async Task CreateHotelAsync_CallsRepositoryAddAndSave()
    {
        var hotel = BuildHotel("manager-1");
        _repoMock.Setup(r => r.AddAsync(It.IsAny<HotelEntity>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        await _service.CreateHotelAsync(hotel, "manager-1");

        _repoMock.Verify(r => r.AddAsync(It.IsAny<HotelEntity>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ── UpdateHotelAsync ──────────────────────────────────────────────────────

    // Happy path — the correct owner updates their hotel and the change is persisted.
    [Test]
    public async Task UpdateHotelAsync_CorrectOwner_UpdatesAndReturnsTrue()
    {
        var hotel = BuildHotel("manager-1");
        _context.Hotels.Add(hotel);
        await _context.SaveChangesAsync();

        var updated = BuildHotel("manager-1");
        updated.Id   = hotel.Id;
        updated.Name = "Updated Name";

        var result = await _service.UpdateHotelAsync(updated, "manager-1");

        Assert.That(result, Is.True);
        var fromDb = await _context.Hotels.FindAsync(hotel.Id);
        Assert.That(fromDb!.Name, Is.EqualTo("Updated Name"));
    }

    // A manager trying to edit someone else's hotel must be blocked — this is the core ownership check.
    [Test]
    public async Task UpdateHotelAsync_WrongOwner_ReturnsFalse()
    {
        var hotel = BuildHotel("manager-1");
        _context.Hotels.Add(hotel);
        await _context.SaveChangesAsync();

        var updated = BuildHotel("manager-1");
        updated.Id  = hotel.Id;

        var result = await _service.UpdateHotelAsync(updated, "manager-WRONG");

        Assert.That(result, Is.False);
    }

    // Trying to update a hotel that doesn't exist should return false, not throw.
    [Test]
    public async Task UpdateHotelAsync_NonExistentHotel_ReturnsFalse()
    {
        var hotel = BuildHotel("manager-1");
        hotel.Id  = Guid.NewGuid();

        var result = await _service.UpdateHotelAsync(hotel, "manager-1");

        Assert.That(result, Is.False);
    }

    /*
     * This is a subtle but important security test. An attacker could try to change the ManagerId
     * of a hotel by including a different ManagerId in the update payload. The service must ignore
     * the incoming ManagerId and preserve the original value from the database. We verify this by
     * checking the database record after the update — the ManagerId must still be "manager-1".
     */
    [Test]
    public async Task UpdateHotelAsync_DoesNotOverwriteManagerId()
    {
        var hotel = BuildHotel("manager-1");
        _context.Hotels.Add(hotel);
        await _context.SaveChangesAsync();

        var updated       = BuildHotel("manager-ATTACKER");
        updated.Id        = hotel.Id;
        updated.ManagerId = "manager-ATTACKER";

        await _service.UpdateHotelAsync(updated, "manager-1");

        var fromDb = await _context.Hotels.FindAsync(hotel.Id);
        Assert.That(fromDb!.ManagerId, Is.EqualTo("manager-1"));
    }

    // ── DeleteHotelAsync ──────────────────────────────────────────────────────

    [Test]
    public async Task DeleteHotelAsync_CorrectOwner_ReturnsTrue()
    {
        var hotel = BuildHotel("manager-1");
        _repoMock.Setup(r => r.GetByIdAsync(hotel.Id)).ReturnsAsync(hotel);
        _repoMock.Setup(r => r.Remove(hotel));
        _repoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _cloudinaryMock.Setup(c => c.DeleteImageAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        var result = await _service.DeleteHotelAsync(hotel.Id, "manager-1");

        Assert.That(result, Is.True);
    }

    /*
     * Wrong owner must be blocked AND the Remove method must never be called.
     * We use Moq's Verify to assert that Remove was never invoked — just checking the return value
     * isn't enough, we want to be sure the deletion didn't happen at all.
     */
    [Test]
    public async Task DeleteHotelAsync_WrongOwner_ReturnsFalse()
    {
        var hotel = BuildHotel("manager-1");
        _repoMock.Setup(r => r.GetByIdAsync(hotel.Id)).ReturnsAsync(hotel);

        var result = await _service.DeleteHotelAsync(hotel.Id, "manager-WRONG");

        Assert.That(result, Is.False);
        _repoMock.Verify(r => r.Remove(It.IsAny<HotelEntity>()), Times.Never);
    }

    [Test]
    public async Task DeleteHotelAsync_NonExistentHotel_ReturnsFalse()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((HotelEntity?)null);

        var result = await _service.DeleteHotelAsync(Guid.NewGuid(), "manager-1");

        Assert.That(result, Is.False);
    }

    /*
     * When a hotel is deleted, its Cloudinary image must also be cleaned up. We verify that
     * DeleteImageAsync was called with the exact URL stored on the hotel — not just any URL.
     * This ensures the service is passing the right value, not a hardcoded string or null.
     */
    [Test]
    public async Task DeleteHotelAsync_WithImage_CallsCloudinaryDelete()
    {
        var hotel = BuildHotel("manager-1");
        hotel.ImageUrl = "https://res.cloudinary.com/test/image/upload/v123/luxevoyage/hotels/abc.jpg";
        _repoMock.Setup(r => r.GetByIdAsync(hotel.Id)).ReturnsAsync(hotel);
        _repoMock.Setup(r => r.Remove(hotel));
        _repoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _cloudinaryMock.Setup(c => c.DeleteImageAsync(hotel.ImageUrl)).Returns(Task.CompletedTask);

        await _service.DeleteHotelAsync(hotel.Id, "manager-1");

        _cloudinaryMock.Verify(c => c.DeleteImageAsync(hotel.ImageUrl), Times.Once);
    }

    // ── GetHotelsByCityAsync ──────────────────────────────────────────────────

    /*
     * The city search uses a Contains() query, so "mumbai" should match both "Mumbai, Maharashtra"
     * and "Navi Mumbai, Maharashtra" but not "Delhi, Delhi". We seed all three and verify the count.
     */
    [Test]
    public async Task GetHotelsByCityAsync_MatchingCity_ReturnsResults()
    {
        var h1 = BuildHotel("m1"); h1.Location = "Mumbai, Maharashtra";
        var h2 = BuildHotel("m1"); h2.Location = "Delhi, Delhi";
        var h3 = BuildHotel("m1"); h3.Location = "Navi Mumbai, Maharashtra";
        _context.Hotels.AddRange(h1, h2, h3);
        await _context.SaveChangesAsync();

        var result = await _service.GetHotelsByCityAsync("mumbai");

        Assert.That(result.Count(), Is.EqualTo(2));
    }

    // The search must be case-insensitive — "JAIPUR" should find "Jaipur, Rajasthan".
    [Test]
    public async Task GetHotelsByCityAsync_CaseInsensitive_ReturnsResults()
    {
        var hotel = BuildHotel("m1"); hotel.Location = "Jaipur, Rajasthan";
        _context.Hotels.Add(hotel);
        await _context.SaveChangesAsync();

        var result = await _service.GetHotelsByCityAsync("JAIPUR");

        Assert.That(result.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task GetHotelsByCityAsync_NoMatch_ReturnsEmpty()
    {
        var hotel = BuildHotel("m1"); hotel.Location = "Mumbai, Maharashtra";
        _context.Hotels.Add(hotel);
        await _context.SaveChangesAsync();

        var result = await _service.GetHotelsByCityAsync("Goa");

        Assert.That(result, Is.Empty);
    }

    // Builds a minimal HotelEntity with just enough fields populated to pass EF validation.
    private static HotelEntity BuildHotel(string managerId) => new()
    {
        Id            = Guid.NewGuid(),
        Name          = "Test Hotel",
        Location      = "Test City, Test State",
        PricePerNight = 10000,
        Currency      = "INR",
        Type          = PropertyType.Hotel,
        Rating        = 4.5m,
        ReviewCount   = 100,
        ManagerId     = managerId
    };
}
