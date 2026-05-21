using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebGoatCore;
using WebGoatCore.Controllers;
using WebGoatCore.Data;
using WebGoatCore.Models;

namespace WebGoat.NET.Tests.Controllers;

[TestFixture]
public class CartControllerTests
{
    private Mock<ProductRepository> _mockProductRepository;
    private CartController _controller;
    private TestSession _testSession;

    [SetUp]
    public void Setup()
    {
        _mockProductRepository = new Mock<ProductRepository>();

        _controller = new CartController(_mockProductRepository.Object);

        // Setup HttpContext with real Session
        _testSession = new TestSession();
        var httpContext = new DefaultHttpContext();
        httpContext.Session = _testSession;

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    #region Index Tests

    [Test]
    public void Index_WithEmptyCart_ReturnsEmptyCart()
    {
        // Act
        var result = _controller.Index() as ViewResult;
        var model = result?.Model as Cart;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.OrderDetails.Count, Is.EqualTo(0));
    }

    [Test]
    public void Index_WithExistingCart_ReturnsCartFromSession()
    {
        // Arrange
        var cart = CreateTestCart();
        _testSession.Set("Cart", cart);

        // Act
        var result = _controller.Index() as ViewResult;
        var model = result?.Model as Cart;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.OrderDetails.Count, Is.EqualTo(1));
        Assert.That(model.SubTotal, Is.EqualTo(20.00));
    }

    #endregion

    #region AddOrder Tests

    [Test]
    public void AddOrder_WithZeroQuantity_RedirectsToProductDetails()
    {
        // Arrange
        int productId = 1;
        short quantity = 0;

        // Act
        var result = _controller.AddOrder(productId, quantity) as RedirectToActionResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ActionName, Is.EqualTo("Details"));
        Assert.That(result.ControllerName, Is.EqualTo("Product"));
        Assert.That(result.RouteValues["productId"], Is.EqualTo(productId));
        Assert.That(result.RouteValues["quantity"], Is.EqualTo(quantity));
    }

    [Test]
    public void AddOrder_WithNegativeQuantity_RedirectsToProductDetails()
    {
        // Arrange
        int productId = 1;
        short quantity = -5;

        // Act
        var result = _controller.AddOrder(productId, quantity) as RedirectToActionResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ActionName, Is.EqualTo("Details"));
        Assert.That(result.ControllerName, Is.EqualTo("Product"));
    }

    [Test]
    public void AddOrder_WithNewProduct_AddsProductToCart()
    {
        // Arrange
        int productId = 1;
        short quantity = 2;
        var product = CreateTestProduct(productId);

        _mockProductRepository.Setup(x => x.GetProductById(productId))
            .Returns(product);

        // Act
        var result = _controller.AddOrder(productId, quantity) as RedirectToActionResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ActionName, Is.EqualTo("Index"));

        // Verify cart was updated in session
        var cart = _testSession.Get<Cart>("Cart");
        Assert.That(cart, Is.Not.Null);
        Assert.That(cart.OrderDetails.ContainsKey(productId), Is.True);
        Assert.That(cart.OrderDetails[productId].Quantity, Is.EqualTo(quantity));
        Assert.That(cart.OrderDetails[productId].ProductId, Is.EqualTo(productId));
        Assert.That(cart.OrderDetails[productId].UnitPrice, Is.EqualTo(product.UnitPrice));
    }

    [Test]
    public void AddOrder_WithExistingProduct_IncrementsQuantity()
    {
        // Arrange
        int productId = 1;
        short initialQuantity = 2;
        short additionalQuantity = 3;
        var product = CreateTestProduct(productId);

        // Setup existing cart with product
        var cart = new Cart();
        cart.OrderDetails.Add(productId, new OrderDetail
        {
            ProductId = productId,
            Quantity = initialQuantity,
            UnitPrice = 10.00,
            Product = product,
            Discount = 0.0F
        });
        _testSession.Set("Cart", cart);

        _mockProductRepository.Setup(x => x.GetProductById(productId))
            .Returns(product);

        // Act
        var result = _controller.AddOrder(productId, additionalQuantity);

        // Assert
        var updatedCart = _testSession.Get<Cart>("Cart");
        Assert.That(updatedCart, Is.Not.Null);
        Assert.That(updatedCart.OrderDetails[productId].Quantity, Is.EqualTo(initialQuantity + additionalQuantity));
    }

    [Test]
    public void AddOrder_SetsCorrectOrderDetailProperties()
    {
        // Arrange
        int productId = 1;
        short quantity = 5;
        var product = CreateTestProduct(productId);

        _mockProductRepository.Setup(x => x.GetProductById(productId))
            .Returns(product);

        // Act
        _controller.AddOrder(productId, quantity);

        // Assert
        var cart = _testSession.Get<Cart>("Cart");
        var orderDetail = cart.OrderDetails[productId];

        Assert.That(orderDetail.ProductId, Is.EqualTo(productId));
        Assert.That(orderDetail.Quantity, Is.EqualTo(quantity));
        Assert.That(orderDetail.UnitPrice, Is.EqualTo(product.UnitPrice));
        Assert.That(orderDetail.Discount, Is.EqualTo(0.0F));
        Assert.That(orderDetail.Product.ProductName, Is.EqualTo(product.ProductName));
        Assert.That(orderDetail.Product.UnitPrice, Is.EqualTo(product.UnitPrice));
        Assert.That(orderDetail.Product.UnitsInStock, Is.EqualTo(product.UnitsInStock));
    }

    [Test]
    public void AddOrder_CallsProductRepositoryGetProductById()
    {
        // Arrange
        int productId = 1;
        short quantity = 1;
        var product = CreateTestProduct(productId);

        _mockProductRepository.Setup(x => x.GetProductById(productId))
            .Returns(product);

        // Act
        _controller.AddOrder(productId, quantity);

        // Assert
        _mockProductRepository.Verify(x => x.GetProductById(productId), Times.Once);
    }

    #endregion

    #region RemoveOrder Tests

    [Test]
    public void RemoveOrder_WithExistingProduct_RemovesProductFromCart()
    {
        // Arrange
        int productId = 1;
        var cart = CreateTestCart();
        _testSession.Set("Cart", cart);

        // Act
        var result = _controller.RemoveOrder(productId) as RedirectToActionResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ActionName, Is.EqualTo("Index"));

        var updatedCart = _testSession.Get<Cart>("Cart");
        Assert.That(updatedCart, Is.Not.Null);
        Assert.That(updatedCart.OrderDetails.ContainsKey(productId), Is.False);
    }

    [Test]
    public void RemoveOrder_WithNonExistentProduct_ReturnsErrorView()
    {
        // Arrange
        int productId = 999;
        var cart = CreateTestCart();
        _testSession.Set("Cart", cart);

        // Act
        var result = _controller.RemoveOrder(productId) as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ViewName, Is.EqualTo("RemoveOrderError"));
        Assert.That(result.Model, Is.TypeOf<string>());
        Assert.That(result.Model.ToString(), Does.Contain("not found in your cart"));
        Assert.That(result.Model.ToString(), Does.Contain(productId.ToString()));
    }

    [Test]
    public void RemoveOrder_WithEmptyCart_ReturnsErrorView()
    {
        // Arrange
        int productId = 1;

        // Act
        var result = _controller.RemoveOrder(productId) as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ViewName, Is.EqualTo("RemoveOrderError"));
        Assert.That(result.Model.ToString(), Does.Contain("not found"));
    }

    [Test]
    public void RemoveOrder_UpdatesCartInSession()
    {
        // Arrange
        int productId1 = 1;
        int productId2 = 2;
        var cart = new Cart();
        cart.OrderDetails.Add(productId1, new OrderDetail
        {
            ProductId = productId1,
            Quantity = 2,
            UnitPrice = 10.00
        });
        cart.OrderDetails.Add(productId2, new OrderDetail
        {
            ProductId = productId2,
            Quantity = 1,
            UnitPrice = 15.00
        });
        _testSession.Set("Cart", cart);

        // Act
        _controller.RemoveOrder(productId1);

        // Assert
        var updatedCart = _testSession.Get<Cart>("Cart");
        Assert.That(updatedCart.OrderDetails.Count, Is.EqualTo(1));
        Assert.That(updatedCart.OrderDetails.ContainsKey(productId2), Is.True);
        Assert.That(updatedCart.OrderDetails.ContainsKey(productId1), Is.False);
    }

    #endregion

    #region AddOrder Additional Tests

    [Test]
    public void AddOrder_WithMultipleDifferentProducts_AddsAllToCart()
    {
        // Arrange
        var product1 = CreateTestProduct(1);
        var product2 = CreateTestProduct(2);

        _mockProductRepository.Setup(x => x.GetProductById(1)).Returns(product1);
        _mockProductRepository.Setup(x => x.GetProductById(2)).Returns(product2);

        // Act
        _controller.AddOrder(1, 2);
        _controller.AddOrder(2, 3);

        // Assert
        var cart = _testSession.Get<Cart>("Cart");
        Assert.That(cart.OrderDetails.Count, Is.EqualTo(2));
        Assert.That(cart.OrderDetails[1].Quantity, Is.EqualTo(2));
        Assert.That(cart.OrderDetails[2].Quantity, Is.EqualTo(3));
    }

    [Test]
    public void AddOrder_WithMaxShortValue_AddsToCart()
    {
        // Arrange
        int productId = 1;
        short maxQuantity = short.MaxValue;
        var product = CreateTestProduct(productId);

        _mockProductRepository.Setup(x => x.GetProductById(productId))
            .Returns(product);

        // Act
        var result = _controller.AddOrder(productId, maxQuantity);

        // Assert
        var cart = _testSession.Get<Cart>("Cart");
        Assert.That(cart.OrderDetails[productId].Quantity, Is.EqualTo(maxQuantity));
    }

    #endregion

    #region RemoveOrder Additional Tests

    [Test]
    public void RemoveOrder_RemovesOnlySpecifiedProduct()
    {
        // Arrange
        var cart = new Cart();
        for (int i = 1; i <= 5; i++)
        {
            cart.OrderDetails.Add(i, new OrderDetail
            {
                ProductId = i,
                Quantity = 1,
                UnitPrice = 10.00 * i
            });
        }
        _testSession.Set("Cart", cart);

        // Act
        _controller.RemoveOrder(3);

        // Assert
        var updatedCart = _testSession.Get<Cart>("Cart");
        Assert.That(updatedCart.OrderDetails.Count, Is.EqualTo(4));
        Assert.That(updatedCart.OrderDetails.ContainsKey(3), Is.False);
        Assert.That(updatedCart.OrderDetails.ContainsKey(1), Is.True);
        Assert.That(updatedCart.OrderDetails.ContainsKey(2), Is.True);
        Assert.That(updatedCart.OrderDetails.ContainsKey(4), Is.True);
        Assert.That(updatedCart.OrderDetails.ContainsKey(5), Is.True);
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
            Product = CreateTestProduct(1),
            Discount = 0.0F
        };
        cart.OrderDetails.Add(1, orderDetail);
        return cart;
    }

    private Product CreateTestProduct(int productId)
    {
        return new Product
        {
            ProductId = productId,
            ProductName = $"Test Product {productId}",
            UnitPrice = 10.00,
            UnitsInStock = 100
        };
    }

    #endregion
}
