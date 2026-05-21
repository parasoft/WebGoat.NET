using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using WebGoatCore;
using WebGoatCore.Controllers;
using WebGoatCore.Data;
using WebGoatCore.Exceptions;
using WebGoatCore.Models;
using WebGoatCore.ViewModels;

namespace WebGoat.NET.Tests.Controllers;

[TestFixture]
public class CheckoutControllerTests
{
    private Mock<UserManager<IdentityUser>> _mockUserManager;
    private Mock<CustomerRepository> _mockCustomerRepository;
    private Mock<ShipperRepository> _mockShipperRepository;
    private Mock<OrderRepository> _mockOrderRepository;
    private Mock<IHostEnvironment> _mockHostEnvironment;
    private Mock<IConfiguration> _mockConfiguration;
    private CheckoutController _controller;
    private TestSession _testSession;
    private const string TestUsername = "testuser";
    private string _testResourcePath;

    // Note: OrderRepository methods are virtual and mocked directly.
    // If you need to test OrderRepository methods that use _context internally,
    // you would need to either:
    // 1. Continue mocking the virtual methods (current approach - recommended)
    // 2. Use an in-memory database for integration testing
    // 3. Make _context injectable and mockable (requires refactoring)

    [SetUp]
    public void Setup()
    {
        // Create a temporary directory for test resources
        _testResourcePath = Path.Combine(Path.GetTempPath(), "WebGoatTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testResourcePath);
        // Setup UserManager mock
        var userStore = new Mock<IUserStore<IdentityUser>>();
        _mockUserManager = new Mock<UserManager<IdentityUser>>(
            userStore.Object, null, null, null, null, null, null, null, null);

        _mockCustomerRepository = new Mock<CustomerRepository>();
        _mockShipperRepository = new Mock<ShipperRepository>();
        _mockOrderRepository = new Mock<OrderRepository>();
        _mockHostEnvironment = new Mock<IHostEnvironment>();
        _mockConfiguration = new Mock<IConfiguration>();

        // Setup configuration
        var mockConfigSection = new Mock<IConfigurationSection>();
        mockConfigSection.Setup(x => x.Value).Returns(_testResourcePath);
        _mockConfiguration.Setup(x => x.GetSection(Constants.WEBGOAT_ROOT)).Returns(mockConfigSection.Object);
        _mockConfiguration.Setup(x => x[Constants.WEBGOAT_ROOT]).Returns(_testResourcePath);
        _mockHostEnvironment.Setup(x => x.ContentRootPath).Returns(_testResourcePath);

        _controller = new CheckoutController(
            _mockUserManager.Object,
            _mockCustomerRepository.Object,
            _mockHostEnvironment.Object,
            _mockConfiguration.Object,
            _mockShipperRepository.Object,
            _mockOrderRepository.Object
        );

        // Setup HttpContext with real Session
        _testSession = new TestSession();
        var httpContext = new DefaultHttpContext();
        httpContext.Session = _testSession;

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, TestUsername)
        }, "mock"));

        httpContext.User = user;

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _mockUserManager.Setup(x => x.GetUserName(It.IsAny<ClaimsPrincipal>()))
            .Returns(TestUsername);
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up the temporary directory
        if (Directory.Exists(_testResourcePath))
        {
            Directory.Delete(_testResourcePath, true);
        }
    }

    #region Checkout GET Tests

    [Test]
    public void Checkout_Get_WithEmptyCart_AddsModelError()
    {
        // Arrange
        SetupCustomer();

        // Act
        var result = _controller.Checkout() as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(_controller.ModelState.IsValid, Is.False);
        Assert.That(_controller.ModelState.ContainsKey(string.Empty), Is.True);
    }

    [Test]
    public void Checkout_Get_WithValidCart_ReturnsViewModel()
    {
        // Arrange
        var cart = CreateTestCart();
        SetupCustomer();
        SetupShippingOptions(cart.SubTotal);

        // Act
        var result = _controller.Checkout() as ViewResult;
        var model = result?.Model as CheckoutViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Cart, Is.Not.Null);
        Assert.That(model.Cart.OrderDetails.Count, Is.EqualTo(cart.OrderDetails.Count));
        Assert.That(model.Cart.SubTotal, Is.EqualTo(cart.SubTotal));
        Assert.That(model.AvailableExpirationYears.Count, Is.EqualTo(5));
    }

    [Test]
    public void Checkout_Get_WithoutCustomer_AddsModelError()
    {
        // Arrange
        var cart = CreateTestCart();
        _mockCustomerRepository.Setup(x => x.GetCustomerByUsername(TestUsername))
            .Returns((Customer)null);

        // Act
        var result = _controller.Checkout() as ViewResult;

        // Assert
        Assert.That(_controller.ModelState.IsValid, Is.False);
        Assert.That(_controller.ModelState[string.Empty]?.Errors[0].ErrorMessage,
            Does.Contain("can't identify you"));
    }

    [Test]
    public void Checkout_Get_PopulatesCustomerInformation()
    {
        // Arrange
        var cart = CreateTestCart();
        var customer = CreateTestCustomer();
        _mockCustomerRepository.Setup(x => x.GetCustomerByUsername(TestUsername))
            .Returns(customer);
        SetupShippingOptions(cart.SubTotal);

        // Act
        var result = _controller.Checkout() as ViewResult;
        var model = result?.Model as CheckoutViewModel;

        // Assert
        Assert.That(model, Is.Not.Null);
        Assert.That(model.ShipTarget, Is.EqualTo(customer.CompanyName));
        Assert.That(model.Address, Is.EqualTo(customer.Address));
        Assert.That(model.City, Is.EqualTo(customer.City));
        Assert.That(model.Region, Is.EqualTo(customer.Region));
        Assert.That(model.PostalCode, Is.EqualTo(customer.PostalCode));
        Assert.That(model.Country, Is.EqualTo(customer.Country));
    }

    [Test]
    public void Checkout_Get_GeneratesExpirationYears()
    {
        // Arrange
        var cart = CreateTestCart();
        SetupCustomer();
        SetupShippingOptions(cart.SubTotal);

        // Act
        var result = _controller.Checkout() as ViewResult;
        var model = result?.Model as CheckoutViewModel;

        // Assert
        Assert.That(model, Is.Not.Null);
        Assert.That(model.AvailableExpirationYears, Is.Not.Null);
        Assert.That(model.AvailableExpirationYears.Count, Is.EqualTo(5));
        Assert.That(model.AvailableExpirationYears.First(), Is.EqualTo(DateTime.Now.Year + 1));
        Assert.That(model.AvailableExpirationYears.Last(), Is.EqualTo(DateTime.Now.Year + 5));
    }

    #endregion

    #region Checkout POST Tests

    [Test]
    public void Checkout_Post_WithoutCustomer_ReturnsViewWithError()
    {
        // Arrange
        var model = CreateCheckoutViewModel();
        var cart = CreateTestCart();
        _mockCustomerRepository.Setup(x => x.GetCustomerByUsername(TestUsername))
            .Returns((Customer)null);

        // Act
        var result = _controller.Checkout(model) as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(_controller.ModelState.IsValid, Is.False);
    }

    [Test]
    public void Checkout_Post_WithShortCardNumber_AddsModelError()
    {
        // Arrange
        var model = CreateCheckoutViewModel();
        model.CreditCard = "123"; // Too short
        var cart = CreateTestCart();
        SetupCustomer();

        // Act
        var result = _controller.Checkout(model) as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(_controller.ModelState.IsValid, Is.False);
        Assert.That(_controller.ModelState[string.Empty]?.Errors[0].ErrorMessage,
            Does.Contain("too short"));
    }

    [Test]
    public void Checkout_Post_WithValidCard_CreatesOrder()
    {
        // Arrange
        var model = CreateCheckoutViewModel();
        var cart = CreateTestCart();
        var customer = CreateTestCustomer();
        var shipper = CreateTestShipper();

        _mockCustomerRepository.Setup(x => x.GetCustomerByUsername(TestUsername))
            .Returns(customer);
        _mockShipperRepository.Setup(x => x.GetShipperByShipperId(It.IsAny<int>()))
            .Returns(shipper);
        _mockShipperRepository.Setup(x => x.GetNextTrackingNumber(shipper))
            .Returns("TRACK123");
        _mockOrderRepository.Setup(x => x.CreateOrder(It.IsAny<Order>()))
            .Returns(100);
        _mockOrderRepository.Setup(x => x.CreateOrderPayment(
            It.IsAny<int>(), 
            It.IsAny<decimal>(), 
            It.IsAny<string>(), 
            It.IsAny<DateTime>(), 
            It.IsAny<string>()));

        // Act
        var result = _controller.Checkout(model) as RedirectToActionResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ActionName, Is.EqualTo("Receipt"));
        _mockOrderRepository.Verify(x => x.CreateOrder(It.IsAny<Order>()), Times.Once);
        _mockOrderRepository.Verify(x => x.CreateOrderPayment(
            It.IsAny<int>(), 
            It.IsAny<decimal>(), 
            It.IsAny<string>(), 
            It.IsAny<DateTime>(), 
            It.IsAny<string>()), Times.Once);
    }

    [Test]
    public void Checkout_Post_ClearsCartAfterSuccessfulOrder()
    {
        // Arrange
        var model = CreateCheckoutViewModel();
        var cart = CreateTestCart();
        var customer = CreateTestCustomer();
        var shipper = CreateTestShipper();

        SetupCustomer(customer);
        _mockShipperRepository.Setup(x => x.GetShipperByShipperId(It.IsAny<int>()))
            .Returns(shipper);
        _mockShipperRepository.Setup(x => x.GetNextTrackingNumber(shipper))
            .Returns("TRACK123");
        _mockOrderRepository.Setup(x => x.CreateOrder(It.IsAny<Order>()))
            .Returns(100);
        _mockOrderRepository.Setup(x => x.CreateOrderPayment(
            It.IsAny<int>(), 
            It.IsAny<decimal>(), 
            It.IsAny<string>(), 
            It.IsAny<DateTime>(), 
            It.IsAny<string>()));

        // Act
        var result = _controller.Checkout(model);

        // Assert - Cart should be removed from session
        Assert.That(_testSession.TryGetValue("Cart", out _), Is.False);
    }

    [Test]
    public void Checkout_Post_SetsOrderIdInSession()
    {
        // Arrange
        var model = CreateCheckoutViewModel();
        var cart = CreateTestCart();
        var customer = CreateTestCustomer();
        var shipper = CreateTestShipper();
        int expectedOrderId = 100;

        SetupCustomer(customer);
        _mockShipperRepository.Setup(x => x.GetShipperByShipperId(It.IsAny<int>()))
            .Returns(shipper);
        _mockShipperRepository.Setup(x => x.GetNextTrackingNumber(shipper))
            .Returns("TRACK123");
        _mockOrderRepository.Setup(x => x.CreateOrder(It.IsAny<Order>()))
            .Returns(expectedOrderId);
        _mockOrderRepository.Setup(x => x.CreateOrderPayment(
            It.IsAny<int>(), 
            It.IsAny<decimal>(), 
            It.IsAny<string>(), 
            It.IsAny<DateTime>(), 
            It.IsAny<string>()));

        // Act
        var result = _controller.Checkout(model);

        // Assert - OrderId should be set in session
        var orderId = _testSession.GetInt32("OrderId");
        Assert.That(orderId, Is.EqualTo(expectedOrderId));
    }

    #endregion

    #region Receipt Tests

    [Test]
    public void Receipt_WithValidOrderId_ReturnsOrder()
    {
        // Arrange
        int orderId = 100;
        var order = CreateTestOrder(orderId);

        _testSession.SetInt32("OrderId", orderId);
        _mockOrderRepository.Setup(x => x.GetOrderById(orderId)).Returns(order);

        // Act
        var result = _controller.Receipt(null) as ViewResult;
        var model = result?.Model as Order;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.OrderId, Is.EqualTo(orderId));
    }

    [Test]
    public void Receipt_WithIdParameter_UsesProvidedId()
    {
        // Arrange
        int orderId = 100;
        var order = CreateTestOrder(orderId);
        
        _mockOrderRepository.Setup(x => x.GetOrderById(orderId)).Returns(order);

        // Act
        var result = _controller.Receipt(orderId) as ViewResult;
        var model = result?.Model as Order;

        // Assert
        Assert.That(model, Is.Not.Null);
        Assert.That(model.OrderId, Is.EqualTo(orderId));
    }

    [Test]
    public void Receipt_WithNoOrderId_AddsModelError()
    {
        // Arrange - Session has no OrderId set

        // Act
        var result = _controller.Receipt(null) as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(_controller.ModelState.IsValid, Is.False);
        Assert.That(_controller.ModelState[string.Empty]?.Errors[0].ErrorMessage,
            Does.Contain("No order specified"));
    }

    [Test]
    public void Receipt_WithInvalidOrderId_AddsModelError()
    {
        // Arrange
        int orderId = 999;
        _testSession.SetInt32("OrderId", orderId);
        _mockOrderRepository.Setup(x => x.GetOrderById(orderId))
            .Throws<InvalidOperationException>();

        // Act
        var result = _controller.Receipt(null) as ViewResult;

        // Assert
        Assert.That(_controller.ModelState.IsValid, Is.False);
        Assert.That(_controller.ModelState[string.Empty]?.Errors[0].ErrorMessage,
            Does.Contain("was not found"));
    }

    #endregion

    #region Receipts Tests

    [Test]
    public void Receipts_WithValidCustomer_ReturnsOrders()
    {
        // Arrange
        var customer = CreateTestCustomer();
        var orders = new List<Order>
        {
            CreateTestOrder(1),
            CreateTestOrder(2)
        };

        _mockCustomerRepository.Setup(x => x.GetCustomerByUsername(TestUsername))
            .Returns(customer);
        _mockOrderRepository.Setup(x => x.GetAllOrdersByCustomerId(customer.CustomerId))
            .Returns(orders);

        // Act
        var result = _controller.Receipts() as ViewResult;
        var model = result?.Model as List<Order>;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Count, Is.EqualTo(2));
    }

    [Test]
    public void Receipts_WithoutCustomer_ReturnsViewWithError()
    {
        // Arrange
        _mockCustomerRepository.Setup(x => x.GetCustomerByUsername(TestUsername))
            .Returns((Customer)null);

        // Act
        var result = _controller.Receipts() as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(_controller.ModelState.IsValid, Is.False);
    }

    #endregion

    #region PackageTracking Tests

    [Test]
    public void PackageTracking_WithValidCustomer_ReturnsViewModel()
    {
        // Arrange
        var customer = CreateTestCustomer();
        var orders = new List<Order> { CreateTestOrder(1) };
        string carrier = "UPS";
        string trackingNumber = "TRACK123";

        _mockCustomerRepository.Setup(x => x.GetCustomerByUsername(TestUsername))
            .Returns(customer);
        _mockOrderRepository.Setup(x => x.GetAllOrdersByCustomerId(customer.CustomerId))
            .Returns(orders);

        // Act
        var result = _controller.PackageTracking(carrier, trackingNumber) as ViewResult;
        var model = result?.Model as PackageTrackingViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.SelectedCarrier, Is.EqualTo(carrier));
        Assert.That(model.SelectedTrackingNumber, Is.EqualTo(trackingNumber));
        Assert.That(model.Orders, Is.Not.Null);
        Assert.That(model.Orders.Count, Is.EqualTo(1));
    }

    [Test]
    public void PackageTracking_WithoutCustomer_ReturnsViewWithoutOrders()
    {
        // Arrange
        _mockCustomerRepository.Setup(x => x.GetCustomerByUsername(TestUsername))
            .Returns((Customer)null);

        // Act
        var result = _controller.PackageTracking(null, null) as ViewResult;
        var model = result?.Model as PackageTrackingViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Orders, Is.Null);
    }

    [Test]
    public void PackageTracking_WithNullParameters_ReturnsEmptyViewModel()
    {
        // Arrange
        var customer = CreateTestCustomer();
        SetupCustomer(customer);
        _mockOrderRepository.Setup(x => x.GetAllOrdersByCustomerId(customer.CustomerId))
            .Returns(new List<Order>());

        // Act
        var result = _controller.PackageTracking(null, null) as ViewResult;
        var model = result?.Model as PackageTrackingViewModel;

        // Assert
        Assert.That(model, Is.Not.Null);
        Assert.That(model.SelectedCarrier, Is.Null);
        Assert.That(model.SelectedTrackingNumber, Is.Null);
    }

    #endregion

    #region GoToExternalTracker Tests

    [Test]
    public void GoToExternalTracker_RedirectsToTrackingUrl()
    {
        // Arrange
        string carrier = "UPS";
        string trackingNumber = "TRACK123";

        // Act
        var result = _controller.GoToExternalTracker(carrier, trackingNumber) as RedirectResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Url, Is.Not.Null);
        Assert.That(result.Url, Does.Contain(trackingNumber));
    }

    #endregion

    #region Helper Methods

    private Cart CreateTestCart()
    {
        var cart = new Cart();
        var orderDetail = new OrderDetail
        {
            ProductId = 1,
            Quantity = 2,
            UnitPrice = 10.00,
            Product = new Product { ProductId = 1, ProductName = "Test Product" }
        };
        cart.OrderDetails.Add(1, orderDetail);

        // Set up the cart in the session
        _testSession.Set("Cart", cart);

        return cart;
    }

    private void SetupCartInSession(Cart cart)
    {
        _testSession.Set("Cart", cart);
    }

    private Customer CreateTestCustomer()
    {
        return new Customer
        {
            CustomerId = "TESTC",
            CompanyName = "Test Company",
            ContactName = TestUsername,
            Address = "123 Test St",
            City = "Test City",
            Region = "Test Region",
            PostalCode = "12345",
            Country = "Test Country"
        };
    }

    private CheckoutViewModel CreateCheckoutViewModel()
    {
        return new CheckoutViewModel
        {
            CreditCard = "4111111111111111",
            ExpirationMonth = 12,
            ExpirationYear = DateTime.Now.Year + 1,
            ShippingMethod = 1,
            ShipTarget = "Test Target",
            Address = "123 Test St",
            City = "Test City",
            Region = "Test Region",
            PostalCode = "12345",
            Country = "Test Country",
            RememberCreditCard = false
        };
    }

    private Order CreateTestOrder(int orderId)
    {
        return new Order
        {
            OrderId = orderId,
            CustomerId = "TESTC",
            OrderDate = DateTime.Now,
            ShipName = "Test Ship",
            OrderDetails = new List<OrderDetail>
            {
                new OrderDetail { ProductId = 1, Quantity = 1, UnitPrice = 10.00 }
            }
        };
    }

    private Shipper CreateTestShipper()
    {
        return new Shipper
        {
            ShipperId = 1,
            CompanyName = "Test Shipper"
        };
    }

    private void SetupCustomer(Customer customer = null)
    {
        customer ??= CreateTestCustomer();
        _mockCustomerRepository.Setup(x => x.GetCustomerByUsername(TestUsername))
            .Returns(customer);
    }

    private void SetupShippingOptions(decimal subtotal)
    {
        _mockShipperRepository.Setup(x => x.GetShippingOptions(subtotal))
            .Returns(new Dictionary<int, string>
            {
                { 1, "Test Shipper Express - 5.00" },
                { 2, "Test Shipper Standard - 3.00" }
            });
    }

    #endregion
}

/// <summary>
/// Simple test implementation of ISession for unit testing
/// </summary>
internal class TestSession : ISession
{
    private readonly Dictionary<string, byte[]> _sessionStorage = new Dictionary<string, byte[]>();

    public bool IsAvailable => true;

    public string Id => Guid.NewGuid().ToString();

    public IEnumerable<string> Keys => _sessionStorage.Keys;

    public void Clear()
    {
        _sessionStorage.Clear();
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void Remove(string key)
    {
        _sessionStorage.Remove(key);
    }

    public void Set(string key, byte[] value)
    {
        _sessionStorage[key] = value;
    }

    public bool TryGetValue(string key, out byte[] value)
    {
        return _sessionStorage.TryGetValue(key, out value);
    }
}