using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WebGoatCore.Controllers;
using WebGoatCore.Data;
using WebGoatCore.Models;
using WebGoatCore.ViewModels;

namespace WebGoat.NET.Tests.Controllers;

[TestFixture]
public class HomeControllerTests
{
    private Mock<ProductRepository> _mockProductRepository;
    private HomeController _controller;

    [SetUp]
    public void Setup()
    {
        _mockProductRepository = new Mock<ProductRepository>();
        _controller = new HomeController(_mockProductRepository.Object);
    }

    #region Index Tests

    [Test]
    public void Index_ReturnsViewWithTopProducts()
    {
        // Arrange
        var topProducts = new List<Product>
        {
            new Product { ProductId = 1, ProductName = "Product 1", UnitPrice = 10.99 },
            new Product { ProductId = 2, ProductName = "Product 2", UnitPrice = 20.99 },
            new Product { ProductId = 3, ProductName = "Product 3", UnitPrice = 30.99 },
            new Product { ProductId = 4, ProductName = "Product 4", UnitPrice = 40.99 }
        };

        _mockProductRepository.Setup(x => x.GetTopProducts(4)).Returns(topProducts);

        // Act
        var result = _controller.Index() as ViewResult;
        var model = result?.Model as HomeViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.TopProducts, Is.EqualTo(topProducts));
        Assert.That(model.TopProducts.Count, Is.EqualTo(4));
    }

    [Test]
    public void Index_RequestsExactlyFourProducts()
    {
        // Arrange
        _mockProductRepository.Setup(x => x.GetTopProducts(4))
            .Returns(new List<Product>());

        // Act
        _controller.Index();

        // Assert
        _mockProductRepository.Verify(x => x.GetTopProducts(4), Times.Once);
    }

    [Test]
    public void Index_WithNoProducts_ReturnsEmptyList()
    {
        // Arrange
        _mockProductRepository.Setup(x => x.GetTopProducts(4))
            .Returns(new List<Product>());

        // Act
        var result = _controller.Index() as ViewResult;
        var model = result?.Model as HomeViewModel;

        // Assert
        Assert.That(model, Is.Not.Null);
        Assert.That(model.TopProducts, Is.Not.Null);
        Assert.That(model.TopProducts.Count, Is.EqualTo(0));
    }

    #endregion

    #region About Tests

    [Test]
    public void About_ReturnsView()
    {
        // Act
        var result = _controller.About() as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    #endregion

    #region Admin Tests

    [Test]
    public void Admin_ReturnsView()
    {
        // Act
        var result = _controller.Admin() as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void Admin_HasAuthorizeAttribute()
    {
        // Arrange
        var methodInfo = typeof(HomeController).GetMethod("Admin");

        // Act
        var attributes = methodInfo.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);
        var authorizeAttr = attributes.FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

        // Assert
        Assert.That(attributes, Is.Not.Empty);
        Assert.That(authorizeAttr?.Roles, Is.EqualTo("Admin"));
    }

    #endregion

    #region Error Tests

    [Test]
    public void Error_ReturnsViewWithErrorViewModel()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = _controller.Error() as ViewResult;
        var model = result?.Model as ErrorViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.RequestId, Is.EqualTo("test-trace-id"));
    }

    [Test]
    public void Error_WithActivity_UsesActivityId()
    {
        // Arrange
        var activity = new Activity("test-activity");
        activity.Start();

        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = _controller.Error() as ViewResult;
        var model = result?.Model as ErrorViewModel;

        // Assert
        Assert.That(model, Is.Not.Null);
        Assert.That(model.RequestId, Is.EqualTo(activity.Id));

        // Cleanup
        activity.Stop();
    }

    [Test]
    public void Error_WithExceptionHandlerFeature_IncludesExceptionInfo()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var exceptionFeature = new Mock<IExceptionHandlerPathFeature>();
        exceptionFeature.Setup(x => x.Path).Returns("/test-path");
        
        httpContext.Features.Set(exceptionFeature.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = _controller.Error() as ViewResult;
        var model = result?.Model as ErrorViewModel;

        // Assert
        Assert.That(model, Is.Not.Null);
        Assert.That(model.ExceptionInfo, Is.Not.Null);
    }

    [Test]
    public void Error_HasResponseCacheAttribute()
    {
        // Arrange
        var methodInfo = typeof(HomeController).GetMethod("Error");

        // Act
        var attributes = methodInfo.GetCustomAttributes(typeof(ResponseCacheAttribute), false);
        var cacheAttr = attributes.FirstOrDefault() as ResponseCacheAttribute;

        // Assert
        Assert.That(attributes, Is.Not.Empty);
        Assert.That(cacheAttr, Is.Not.Null);
        Assert.That(cacheAttr.Duration, Is.EqualTo(0));
        Assert.That(cacheAttr.Location, Is.EqualTo(ResponseCacheLocation.None));
        Assert.That(cacheAttr.NoStore, Is.True);
    }

    #endregion

    #region Authorization Tests

    [Test]
    public void HomeController_HasAllowAnonymousAttribute()
    {
        // Arrange
        var controllerType = typeof(HomeController);

        // Act
        var attributes = controllerType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), false);

        // Assert
        Assert.That(attributes, Is.Not.Empty, "HomeController should have AllowAnonymous attribute");
    }

    #endregion
}