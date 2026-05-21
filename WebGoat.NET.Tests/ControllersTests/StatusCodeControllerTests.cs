using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using WebGoatCore.Controllers;
using WebGoatCore.ViewModels;

namespace WebGoat.NET.Tests.Controllers;

[TestFixture]
public class StatusCodeControllerTests
{
    private StatusCodeController _controller;

    [SetUp]
    public void Setup()
    {
        _controller = new StatusCodeController();
    }

    [Test]
    public void StatusCodeView_ReturnsViewResult()
    {
        // Arrange
        int statusCode = 404;

        // Act
        var result = _controller.StatusCodeView(statusCode);

        // Assert
        Assert.That(result, Is.InstanceOf<ViewResult>());
    }

    [Test]
    public void StatusCodeView_SetsCorrectStatusCode()
    {
        // Arrange
        int statusCode = 404;

        // Act
        var result = _controller.StatusCodeView(statusCode) as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.StatusCode, Is.EqualTo(statusCode));
    }

    [Test]
    public void StatusCodeView_ReturnsStatusCodeViewModel()
    {
        // Arrange
        int statusCode = 500;

        // Act
        var result = _controller.StatusCodeView(statusCode) as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Model, Is.InstanceOf<StatusCodeViewModel>());
    }

    [Test]
    public void StatusCodeView_ViewModelHasCorrectCode()
    {
        // Arrange
        int statusCode = 403;

        // Act
        var result = _controller.StatusCodeView(statusCode) as ViewResult;
        var model = result?.Model as StatusCodeViewModel;

        // Assert
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Code, Is.EqualTo(statusCode));
    }

    [Test]
    public void StatusCodeView_ViewModelHasMessage()
    {
        // Arrange
        int statusCode = 404;

        // Act
        var result = _controller.StatusCodeView(statusCode) as ViewResult;
        var model = result?.Model as StatusCodeViewModel;

        // Assert
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Message, Is.Not.Null);
        Assert.That(model.Message, Is.Not.Empty);
    }

    [Test]
    [TestCase(200)]
    [TestCase(404)]
    [TestCase(500)]
    [TestCase(403)]
    [TestCase(401)]
    public void StatusCodeView_HandlesMultipleStatusCodes(int statusCode)
    {
        // Act
        var result = _controller.StatusCodeView(statusCode) as ViewResult;
        var model = result?.Model as StatusCodeViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.StatusCode, Is.EqualTo(statusCode));
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Code, Is.EqualTo(statusCode));
    }

    [Test]
    public void StatusCodeController_HasAllowAnonymousAttribute()
    {
        // Arrange
        var controllerType = typeof(StatusCodeController);

        // Act
        var attributes = controllerType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), false);

        // Assert
        Assert.That(attributes, Is.Not.Empty);
    }

    [Test]
    public void StatusCodeController_HasIgnoreAntiforgeryTokenAttribute()
    {
        // Arrange
        var controllerType = typeof(StatusCodeController);

        // Act
        var attributes = controllerType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryTokenAttribute), false);

        // Assert
        Assert.That(attributes, Is.Not.Empty);
    }
}