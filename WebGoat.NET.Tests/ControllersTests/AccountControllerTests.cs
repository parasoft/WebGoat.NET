using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using WebGoatCore;
using WebGoatCore.Controllers;
using WebGoatCore.Data;
using WebGoatCore.Models;
using WebGoatCore.ViewModels;

namespace WebGoat.NET.Tests.Controllers;

[TestFixture]
public class AccountControllerTests
{
    private Mock<UserManager<IdentityUser>> _mockUserManager;
    private Mock<SignInManager<IdentityUser>> _mockSignInManager;
    private Mock<CustomerRepository> _mockCustomerRepository;
    private AccountController _controller;
    private TestSession _testSession;
    private const string TestUsername = "testuser";
    private const string TestEmail = "test@example.com";
    private const string TestPassword = "Test@123";

    [SetUp]
    public void Setup()
    {
        // Setup UserManager mock
        var userStore = new Mock<IUserStore<IdentityUser>>();
        _mockUserManager = new Mock<UserManager<IdentityUser>>(
            userStore.Object, null, null, null, null, null, null, null, null);

        // Setup SignInManager mock
        var contextAccessor = new Mock<IHttpContextAccessor>();
        var userPrincipalFactory = new Mock<IUserClaimsPrincipalFactory<IdentityUser>>();
        _mockSignInManager = new Mock<SignInManager<IdentityUser>>(
            _mockUserManager.Object,
            contextAccessor.Object,
            userPrincipalFactory.Object,
            null, null, null, null);

        _mockCustomerRepository = new Mock<CustomerRepository>();

        _controller = new AccountController(
            _mockUserManager.Object,
            _mockSignInManager.Object,
            _mockCustomerRepository.Object
        );

        // Setup HttpContext with Session
        _testSession = new TestSession();
        var httpContext = new DefaultHttpContext();
        httpContext.Session = _testSession;

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, TestUsername),
            new Claim(ClaimTypes.Role, "User")
        }, "mock"));

        httpContext.User = user;

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _mockUserManager.Setup(x => x.GetUserName(It.IsAny<ClaimsPrincipal>()))
            .Returns(TestUsername);
    }

    #region Login GET Tests

    [Test]
    public void Login_Get_ReturnsViewWithModel()
    {
        // Arrange
        string returnUrl = "/Home/Index";

        // Act
        var result = _controller.Login(returnUrl) as ViewResult;
        var model = result?.Model as LoginViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.ReturnUrl, Is.EqualTo(returnUrl));
    }

    [Test]
    public void Login_Get_WithNullReturnUrl_ReturnsViewWithNullReturnUrl()
    {
        // Act
        var result = _controller.Login((string)null) as ViewResult;
        var model = result?.Model as LoginViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.ReturnUrl, Is.Null);
    }

    #endregion

    #region Login POST Tests

    [Test]
    public async Task Login_Post_WithInvalidModelState_ReturnsViewWithModel()
    {
        // Arrange
        var model = new LoginViewModel
        {
            Username = TestUsername,
            Password = TestPassword
        };
        _controller.ModelState.AddModelError("test", "test error");

        // Act
        var result = await _controller.Login(model) as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Model, Is.EqualTo(model));
    }

    [Test]
    public async Task Login_Post_WithValidCredentials_RedirectsToReturnUrl()
    {
        // Arrange
        var model = new LoginViewModel
        {
            Username = TestUsername,
            Password = TestPassword,
            RememberMe = false,
            ReturnUrl = "/Checkout"
        };

        _mockSignInManager.Setup(x => x.PasswordSignInAsync(
            TestUsername,
            TestPassword,
            false,
            true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        // Act
        var result = await _controller.Login(model) as RedirectResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Url, Is.EqualTo("/Checkout"));
    }

    [Test]
    public async Task Login_Post_WithValidCredentialsAndNullReturnUrl_RedirectsToHome()
    {
        // Arrange
        var model = new LoginViewModel
        {
            Username = TestUsername,
            Password = TestPassword,
            RememberMe = true,
            ReturnUrl = null
        };

        _mockSignInManager.Setup(x => x.PasswordSignInAsync(
            TestUsername,
            TestPassword,
            true,
            true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        // Act
        var result = await _controller.Login(model) as RedirectToActionResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ActionName, Is.EqualTo("Index"));
        Assert.That(result.ControllerName, Is.EqualTo("Home"));
    }

    [Test]
    public async Task Login_Post_WithLockedOutAccount_RedirectsToLockoutPage()
    {
        // Arrange
        var model = new LoginViewModel
        {
            Username = TestUsername,
            Password = TestPassword
        };

        _mockSignInManager.Setup(x => x.PasswordSignInAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        // Act
        var result = await _controller.Login(model) as RedirectToPageResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.PageName, Is.EqualTo("./Lockout"));
    }

    [Test]
    public async Task Login_Post_WithInvalidCredentials_ReturnsViewWithError()
    {
        // Arrange
        var model = new LoginViewModel
        {
            Username = TestUsername,
            Password = "WrongPassword"
        };

        _mockSignInManager.Setup(x => x.PasswordSignInAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        // Act
        var result = await _controller.Login(model) as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Model, Is.EqualTo(model));
        Assert.That(_controller.ModelState.IsValid, Is.False);
        Assert.That(_controller.ModelState[string.Empty]?.Errors[0].ErrorMessage,
            Does.Contain("Invalid login attempt"));
    }

    #endregion

    #region Logout Tests

    [Test]
    public async Task Logout_SignsOutUserAndClearsCart()
    {
        // Arrange
        var cart = new Cart();
        cart.OrderDetails.Add(1, new OrderDetail { ProductId = 1, Quantity = 2 });
        _testSession.Set("Cart", cart);

        // Act
        var result = await _controller.Logout() as RedirectToActionResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ActionName, Is.EqualTo("Index"));
        Assert.That(result.ControllerName, Is.EqualTo("Home"));
        _mockSignInManager.Verify(x => x.SignOutAsync(), Times.Once);

        var sessionCart = _testSession.Get<Cart>("Cart");
        Assert.That(sessionCart, Is.Not.Null);
        Assert.That(sessionCart.OrderDetails.Count, Is.EqualTo(0));
    }

    #endregion

    #region Register GET Tests

    [Test]
    public async Task Register_Get_SignsOutExistingUserAndReturnsView()
    {
        // Act
        var result = await _controller.Register() as ViewResult;
        var model = result?.Model as RegisterViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        _mockSignInManager.Verify(x => x.SignOutAsync(), Times.Once);
    }

    #endregion

    #region Register POST Tests

    [Test]
    public async Task Register_Post_WithValidModel_CreatesUserAndCustomer()
    {
        // Arrange
        var model = new RegisterViewModel
        {
            Username = TestUsername,
            Email = TestEmail,
            Password = TestPassword,
            ConfirmedPassword = TestPassword,
            CompanyName = "Test Company",
            Address = "123 Test St",
            City = "Test City",
            Region = "Test Region",
            PostalCode = "12345",
            Country = "Test Country"
        };

        _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<IdentityUser>(), TestPassword))
            .ReturnsAsync(IdentityResult.Success);
        _mockSignInManager.Setup(x => x.SignInAsync(It.IsAny<IdentityUser>(), false, null))
            .Returns(Task.CompletedTask);
        _mockCustomerRepository.Setup(x => x.CreateCustomer(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()))
            .Returns("TESTC");

        // Act
        var result = await _controller.Register(model) as RedirectToActionResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ActionName, Is.EqualTo("Index"));
        Assert.That(result.ControllerName, Is.EqualTo("Home"));
        _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<IdentityUser>(), TestPassword), Times.Once);
        _mockCustomerRepository.Verify(x => x.CreateCustomer(
            model.CompanyName,
            model.Username,
            model.Address,
            model.City,
            model.Region,
            model.PostalCode,
            model.Country), Times.Once);
        _mockSignInManager.Verify(x => x.SignInAsync(It.IsAny<IdentityUser>(), false, null), Times.Once);
    }

    [Test]
    public async Task Register_Post_WithInvalidModelState_ReturnsViewWithModel()
    {
        // Arrange
        var model = new RegisterViewModel();
        _controller.ModelState.AddModelError("test", "test error");

        // Act
        var result = await _controller.Register(model) as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Model, Is.EqualTo(model));
        _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Register_Post_WithUserCreationFailure_ReturnsViewWithErrors()
    {
        // Arrange
        var model = new RegisterViewModel
        {
            Username = TestUsername,
            Email = TestEmail,
            Password = TestPassword,
            ConfirmedPassword = TestPassword,
            CompanyName = "Test Company"
        };

        var errors = new[]
        {
            new IdentityError { Description = "Username already exists" }
        };

        _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<IdentityUser>(), TestPassword))
            .ReturnsAsync(IdentityResult.Failed(errors));

        // Act
        var result = await _controller.Register(model) as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Model, Is.EqualTo(model));
        Assert.That(_controller.ModelState.IsValid, Is.False);
        Assert.That(_controller.ModelState[string.Empty]?.Errors[0].ErrorMessage,
            Does.Contain("Username already exists"));
    }

    #endregion

    #region MyAccount Tests

    [Test]
    public void MyAccount_ReturnsView()
    {
        // Act
        var result = _controller.MyAccount() as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    #endregion

    #region ViewAccountInfo Tests

    [Test]
    public void ViewAccountInfo_WithValidCustomer_ReturnsViewWithCustomer()
    {
        // Arrange
        var customer = CreateTestCustomer();
        _mockCustomerRepository.Setup(x => x.GetCustomerByUsername(TestUsername))
            .Returns(customer);

        // Act
        var result = _controller.ViewAccountInfo() as ViewResult;
        var model = result?.Model as Customer;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.CustomerId, Is.EqualTo(customer.CustomerId));
        Assert.That(_controller.ModelState.IsValid, Is.True);
    }

    [Test]
    public void ViewAccountInfo_WithNoCustomer_ReturnsViewWithError()
    {
        // Arrange
        _mockCustomerRepository.Setup(x => x.GetCustomerByUsername(TestUsername))
            .Returns((Customer)null);

        // Act
        var result = _controller.ViewAccountInfo() as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(_controller.ModelState.IsValid, Is.False);
        Assert.That(_controller.ModelState[string.Empty]?.Errors[0].ErrorMessage,
            Does.Contain("don't recognize your customer Id"));
    }

    #endregion

    #region ChangeAccountInfo GET Tests

    [Test]
    public void ChangeAccountInfo_Get_WithValidCustomer_ReturnsViewWithPopulatedModel()
    {
        // Arrange
        var customer = CreateTestCustomer();
        _mockCustomerRepository.Setup(x => x.GetCustomerByUsername(TestUsername))
            .Returns(customer);

        // Act
        var result = _controller.ChangeAccountInfo() as ViewResult;
        var model = result?.Model as ChangeAccountInfoViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.CompanyName, Is.EqualTo(customer.CompanyName));
        Assert.That(model.Address, Is.EqualTo(customer.Address));
        Assert.That(model.City, Is.EqualTo(customer.City));
        Assert.That(model.Region, Is.EqualTo(customer.Region));
        Assert.That(model.PostalCode, Is.EqualTo(customer.PostalCode));
        Assert.That(model.Country, Is.EqualTo(customer.Country));
    }

    [Test]
    public void ChangeAccountInfo_Get_WithNoCustomer_ReturnsViewWithError()
    {
        // Arrange
        _mockCustomerRepository.Setup(x => x.GetCustomerByUsername(TestUsername))
            .Returns((Customer)null);

        // Act
        var result = _controller.ChangeAccountInfo() as ViewResult;
        var model = result?.Model as ChangeAccountInfoViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(_controller.ModelState.IsValid, Is.False);
    }

    #endregion

    #region ChangeAccountInfo POST Tests

    [Test]
    public void ChangeAccountInfo_Post_WithValidModel_UpdatesCustomer()
    {
        // Arrange
        var customer = CreateTestCustomer();
        var model = new ChangeAccountInfoViewModel
        {
            CompanyName = "Updated Company",
            ContactTitle = "CEO",
            Address = "456 New St",
            City = "New City",
            Region = "New Region",
            PostalCode = "54321",
            Country = "New Country"
        };

        _mockCustomerRepository.Setup(x => x.GetCustomerByUsername(TestUsername))
            .Returns(customer);
        _mockCustomerRepository.Setup(x => x.SaveCustomer(It.IsAny<Customer>()));

        // Act
        var result = _controller.ChangeAccountInfo(model) as ViewResult;
        var resultModel = result?.Model as ChangeAccountInfoViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(resultModel, Is.Not.Null);
        Assert.That(resultModel.UpdatedSucessfully, Is.True);
        _mockCustomerRepository.Verify(x => x.SaveCustomer(It.Is<Customer>(c =>
            c.CompanyName == model.CompanyName &&
            c.ContactTitle == model.ContactTitle &&
            c.Address == model.Address &&
            c.City == model.City &&
            c.Region == model.Region &&
            c.PostalCode == model.PostalCode &&
            c.Country == model.Country)), Times.Once);
    }

    [Test]
    public void ChangeAccountInfo_Post_WithNoCustomer_ReturnsViewWithError()
    {
        // Arrange
        var model = new ChangeAccountInfoViewModel();
        _mockCustomerRepository.Setup(x => x.GetCustomerByUsername(TestUsername))
            .Returns((Customer)null);

        // Act
        var result = _controller.ChangeAccountInfo(model) as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(_controller.ModelState.IsValid, Is.False);
        _mockCustomerRepository.Verify(x => x.SaveCustomer(It.IsAny<Customer>()), Times.Never);
    }

    [Test]
    public void ChangeAccountInfo_Post_WithInvalidModelState_DoesNotSaveCustomer()
    {
        // Arrange
        var customer = CreateTestCustomer();
        var model = new ChangeAccountInfoViewModel();
        _controller.ModelState.AddModelError("test", "test error");
        _mockCustomerRepository.Setup(x => x.GetCustomerByUsername(TestUsername))
            .Returns(customer);

        // Act
        var result = _controller.ChangeAccountInfo(model) as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        _mockCustomerRepository.Verify(x => x.SaveCustomer(It.IsAny<Customer>()), Times.Never);
    }

    [Test]
    public void ChangeAccountInfo_Post_WithNullValues_PreservesExistingData()
    {
        // Arrange
        var customer = CreateTestCustomer();
        var originalCompanyName = customer.CompanyName;
        var originalAddress = customer.Address;

        var model = new ChangeAccountInfoViewModel
        {
            CompanyName = null,  // Should preserve existing
            Address = null,      // Should preserve existing
            City = "New City"    // Should update
        };

        _mockCustomerRepository.Setup(x => x.GetCustomerByUsername(TestUsername))
            .Returns(customer);
        _mockCustomerRepository.Setup(x => x.SaveCustomer(It.IsAny<Customer>()));

        // Act
        _controller.ChangeAccountInfo(model);

        // Assert
        _mockCustomerRepository.Verify(x => x.SaveCustomer(It.Is<Customer>(c =>
            c.CompanyName == originalCompanyName &&
            c.Address == originalAddress &&
            c.City == "New City")), Times.Once);
    }

    #endregion

    #region ChangePassword GET Tests

    [Test]
    public void ChangePassword_Get_ReturnsViewWithModel()
    {
        // Act
        var result = _controller.ChangePassword() as ViewResult;
        var model = result?.Model as ChangePasswordViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
    }

    #endregion

    #region ChangePassword POST Tests

    [Test]
    public async Task ChangePassword_Post_WithValidPassword_ReturnsSuccessView()
    {
        // Arrange
        var model = new ChangePasswordViewModel
        {
            OldPassword = "OldPass@123",
            NewPassword = "NewPass@123",
            ConfirmedNewPassword = "NewPass@123"
        };

        var user = new IdentityUser(TestUsername);
        _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, model.OldPassword, model.NewPassword))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _controller.ChangePassword(model) as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ViewName, Is.EqualTo("ChangePasswordSuccess"));
        _mockUserManager.Verify(x => x.ChangePasswordAsync(user, model.OldPassword, model.NewPassword), Times.Once);
    }

    [Test]
    public async Task ChangePassword_Post_WithInvalidOldPassword_ReturnsViewWithErrors()
    {
        // Arrange
        var model = new ChangePasswordViewModel
        {
            OldPassword = "WrongOldPass",
            NewPassword = "NewPass@123",
            ConfirmedNewPassword = "NewPass@123"
        };

        var user = new IdentityUser(TestUsername);
        var errors = new[]
        {
            new IdentityError { Description = "Incorrect password" }
        };

        _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, model.OldPassword, model.NewPassword))
            .ReturnsAsync(IdentityResult.Failed(errors));

        // Act
        var result = await _controller.ChangePassword(model) as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Model, Is.EqualTo(model));
        Assert.That(_controller.ModelState.IsValid, Is.False);
        Assert.That(_controller.ModelState[string.Empty]?.Errors[0].ErrorMessage,
            Does.Contain("Incorrect password"));
    }

    [Test]
    public async Task ChangePassword_Post_WithInvalidModelState_ReturnsViewWithModel()
    {
        // Arrange
        var model = new ChangePasswordViewModel();
        _controller.ModelState.AddModelError("test", "test error");

        // Act
        var result = await _controller.ChangePassword(model) as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Model, Is.EqualTo(model));
        _mockUserManager.Verify(x => x.ChangePasswordAsync(It.IsAny<IdentityUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region AddUserTemp GET Tests

    [Test]
    public void AddUserTemp_Get_WithAdminUser_ReturnsViewWithAdminFlag()
    {
        // Arrange
        var adminUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.Role, "Admin")
        }, "mock"));
        _controller.ControllerContext.HttpContext.User = adminUser;

        // Act
        var result = _controller.AddUserTemp() as ViewResult;
        var model = result?.Model as AddUserTempViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.IsIssuerAdmin, Is.True);
    }

    [Test]
    public void AddUserTemp_Get_WithNonAdminUser_ReturnsViewWithoutAdminFlag()
    {
        // Act
        var result = _controller.AddUserTemp() as ViewResult;
        var model = result?.Model as AddUserTempViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.IsIssuerAdmin, Is.False);
    }

    #endregion

    #region AddUserTemp POST Tests

    [Test]
    public async Task AddUserTemp_Post_WithNonAdminIssuer_RedirectsToLogin()
    {
        // Arrange
        var model = new AddUserTempViewModel
        {
            IsIssuerAdmin = false,
            NewUsername = "newuser"
        };

        // Act
        var result = await _controller.AddUserTemp(model) as RedirectToActionResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ActionName, Is.EqualTo("Login"));
        _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task AddUserTemp_Post_WithValidModel_CreatesUser()
    {
        // Arrange
        var model = new AddUserTempViewModel
        {
            IsIssuerAdmin = true,
            NewUsername = "newuser",
            NewEmail = "newuser@example.com",
            NewPassword = "NewPass@123",
            MakeNewUserAdmin = false
        };

        _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<IdentityUser>(), model.NewPassword))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _controller.AddUserTemp(model) as ViewResult;
        var resultModel = result?.Model as AddUserTempViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(resultModel, Is.Not.Null);
        Assert.That(resultModel.CreatedUser, Is.True);
        _mockUserManager.Verify(x => x.CreateAsync(
            It.Is<IdentityUser>(u => u.UserName == model.NewUsername && u.Email == model.NewEmail),
            model.NewPassword), Times.Once);
    }

    [Test]
    public async Task AddUserTemp_Post_WithMakeAdminFlag_AddsUserToAdminRole()
    {
        // Arrange
        var model = new AddUserTempViewModel
        {
            IsIssuerAdmin = true,
            NewUsername = "newadmin",
            NewEmail = "newadmin@example.com",
            NewPassword = "NewPass@123",
            MakeNewUserAdmin = true
        };

        _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<IdentityUser>(), model.NewPassword))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.AddToRoleAsync(It.IsAny<IdentityUser>(), "admin"))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _controller.AddUserTemp(model) as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        _mockUserManager.Verify(x => x.AddToRoleAsync(
            It.Is<IdentityUser>(u => u.UserName == model.NewUsername),
            "admin"), Times.Once);
    }

    [Test]
    public async Task AddUserTemp_Post_WithUserCreationFailure_ReturnsViewWithErrors()
    {
        // Arrange
        var model = new AddUserTempViewModel
        {
            IsIssuerAdmin = true,
            NewUsername = "newuser",
            NewEmail = "newuser@example.com",
            NewPassword = "weak"
        };

        var errors = new[]
        {
            new IdentityError { Description = "Password too weak" }
        };

        _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<IdentityUser>(), model.NewPassword))
            .ReturnsAsync(IdentityResult.Failed(errors));

        // Act
        var result = await _controller.AddUserTemp(model) as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(_controller.ModelState.IsValid, Is.False);
        Assert.That(_controller.ModelState[string.Empty]?.Errors[0].ErrorMessage,
            Does.Contain("Password too weak"));
    }

    #endregion

    #region Helper Methods

    private Customer CreateTestCustomer()
    {
        return new Customer
        {
            CustomerId = "TESTC",
            CompanyName = "Test Company",
            ContactName = TestUsername,
            ContactTitle = "Manager",
            Address = "123 Test St",
            City = "Test City",
            Region = "Test Region",
            PostalCode = "12345",
            Country = "Test Country"
        };
    }

    #endregion
}
