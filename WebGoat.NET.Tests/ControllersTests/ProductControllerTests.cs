using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using WebGoatCore.Controllers;
using WebGoatCore.Data;
using WebGoatCore.Models;
using WebGoatCore.ViewModels;

namespace WebGoat.NET.Tests.Controllers;

[TestFixture]
public class ProductControllerTests
{
    private Mock<ProductRepository> _mockProductRepository;
    private Mock<CategoryRepository> _mockCategoryRepository;
    private Mock<SupplierRepository> _mockSupplierRepository;
    private Mock<IWebHostEnvironment> _mockWebHostEnvironment;
    private Mock<IFileProvider> _mockFileProvider;
    private ProductController _controller;

    [SetUp]
    public void Setup()
    {
        _mockProductRepository = new Mock<ProductRepository>();
        _mockCategoryRepository = new Mock<CategoryRepository>();
        _mockSupplierRepository = new Mock<SupplierRepository>();
        _mockWebHostEnvironment = new Mock<IWebHostEnvironment>();
        _mockFileProvider = new Mock<IFileProvider>();

        _mockWebHostEnvironment.Setup(x => x.WebRootFileProvider).Returns(_mockFileProvider.Object);

        _controller = new ProductController(
            _mockProductRepository.Object,
            _mockWebHostEnvironment.Object,
            _mockCategoryRepository.Object,
            _mockSupplierRepository.Object
        );
    }

    #region Search Tests

    [Test]
    public void Search_WithoutFilters_ReturnsAllProducts()
    {
        // Arrange
        var products = new List<Product>
        {
            new Product { ProductId = 1, ProductName = "Product 1" },
            new Product { ProductId = 2, ProductName = "Product 2" }
        };

        _mockProductRepository.Setup(x => x.FindNonDiscontinuedProducts(null, null))
            .Returns(products.ToList());
        _mockCategoryRepository.Setup(x => x.GetAllCategories())
            .Returns(new List<Category>());

        SetupFileProvider("/Images/ProductImages/1.jpg", true);
        SetupFileProvider("/Images/ProductImages/2.jpg", true);

        // Act
        var result = _controller.Search(null, null) as ViewResult;
        var model = result?.Model as ProductListViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Products.Count(), Is.EqualTo(2));
    }

    [Test]
    public void Search_WithNameFilter_ReturnsFilteredProducts()
    {
        // Arrange
        string nameFilter = "Test";
        var products = new List<Product>
        {
            new Product { ProductId = 1, ProductName = "Test Product" }
        };

        _mockProductRepository.Setup(x => x.FindNonDiscontinuedProducts(nameFilter, null))
            .Returns(products.ToList());
        _mockCategoryRepository.Setup(x => x.GetAllCategories())
            .Returns(new List<Category>());

        SetupFileProvider("/Images/ProductImages/1.jpg", true);

        // Act
        var result = _controller.Search(nameFilter, null) as ViewResult;
        var model = result?.Model as ProductListViewModel;

        // Assert
        Assert.That(model, Is.Not.Null);
        Assert.That(model.NameFilter, Is.EqualTo(nameFilter));
        Assert.That(model.Products.Count(), Is.EqualTo(1));
    }

    [Test]
    public void Search_WithValidCategoryId_ReturnsFilteredProducts()
    {
        // Arrange
        int categoryId = 5;
        var products = new List<Product>
        {
            new Product { ProductId = 1, ProductName = "Product 1", CategoryId = categoryId }
        };
        var category = new Category { CategoryId = categoryId, CategoryName = "Test Category" };

        _mockCategoryRepository.Setup(x => x.GetById(categoryId)).Returns(category);
        _mockProductRepository.Setup(x => x.FindNonDiscontinuedProducts(null, categoryId))
            .Returns(products.ToList());
        _mockCategoryRepository.Setup(x => x.GetAllCategories())
            .Returns(new List<Category> { category });

        SetupFileProvider("/Images/ProductImages/1.jpg", true);

        // Act
        var result = _controller.Search(null, categoryId) as ViewResult;
        var model = result?.Model as ProductListViewModel;

        // Assert
        Assert.That(model, Is.Not.Null);
        Assert.That(model.SelectedCategoryId, Is.EqualTo(categoryId));
        Assert.That(model.Products.Count(), Is.EqualTo(1));
    }

    [Test]
    public void Search_WithInvalidCategoryId_IgnoresCategory()
    {
        // Arrange
        int invalidCategoryId = 999;
        var products = new List<Product>
        {
            new Product { ProductId = 1, ProductName = "Product 1" }
        };

        _mockCategoryRepository.Setup(x => x.GetById(invalidCategoryId)).Returns((Category)null);
        _mockProductRepository.Setup(x => x.FindNonDiscontinuedProducts(null, null))
            .Returns(products.ToList());
        _mockCategoryRepository.Setup(x => x.GetAllCategories())
            .Returns(new List<Category>());

        SetupFileProvider("/Images/ProductImages/1.jpg", true);

        // Act
        var result = _controller.Search(null, invalidCategoryId) as ViewResult;
        var model = result?.Model as ProductListViewModel;

        // Assert
        Assert.That(model, Is.Not.Null);
        Assert.That(model.SelectedCategoryId, Is.Null);
    }

    [Test]
    public void Search_ProductWithoutImage_UsesDefaultImage()
    {
        // Arrange
        var products = new List<Product>
        {
            new Product { ProductId = 1, ProductName = "Product Without Image" }
        };

        _mockProductRepository.Setup(x => x.FindNonDiscontinuedProducts(null, null))
            .Returns(products.ToList());
        _mockCategoryRepository.Setup(x => x.GetAllCategories())
            .Returns(new List<Category>());

        SetupFileProvider("/Images/ProductImages/1.jpg", false);
        SetupFileProvider("/Images/ProductImages/NoImage.jpg", true);

        // Act
        var result = _controller.Search(null, null) as ViewResult;
        var model = result?.Model as ProductListViewModel;

        // Assert
        Assert.That(model, Is.Not.Null);
        var productItem = model.Products.First();
        Assert.That(productItem.ImageUrl, Is.EqualTo("/Images/ProductImages/NoImage.jpg"));
    }

    #endregion

    #region Details Tests

    [Test]
    public void Details_WithValidProductId_ReturnsProductDetails()
    {
        // Arrange
        int productId = 1;
        var product = new Product { ProductId = productId, ProductName = "Test Product" };

        _mockProductRepository.Setup(x => x.GetProductById(productId)).Returns(product);
        SetupFileProvider($"/Images/ProductImages/{productId}.jpg", true);

        // Act
        var result = _controller.Details(productId) as ViewResult;
        var model = result?.Model as ProductDetailsViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Product, Is.EqualTo(product));
        Assert.That(model.CanAddToCart, Is.True);
        Assert.That(model.Quantity, Is.EqualTo(1));
        Assert.That(model.ErrorMessage, Is.Null);
    }

    [Test]
    public void Details_WithCustomQuantity_SetsQuantity()
    {
        // Arrange
        int productId = 1;
        short quantity = 5;
        var product = new Product { ProductId = productId, ProductName = "Test Product" };

        _mockProductRepository.Setup(x => x.GetProductById(productId)).Returns(product);
        SetupFileProvider($"/Images/ProductImages/{productId}.jpg", true);

        // Act
        var result = _controller.Details(productId, quantity) as ViewResult;
        var model = result?.Model as ProductDetailsViewModel;

        // Assert
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Quantity, Is.EqualTo(quantity));
    }

    [Test]
    public void Details_ProductNotFound_ReturnsErrorMessage()
    {
        // Arrange
        int productId = 999;
        _mockProductRepository.Setup(x => x.GetProductById(productId))
            .Throws<InvalidOperationException>();

        // Act
        var result = _controller.Details(productId) as ViewResult;
        var model = result?.Model as ProductDetailsViewModel;

        // Assert
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Product, Is.Null);
        Assert.That(model.ErrorMessage, Is.EqualTo("Product not found."));
    }

    [Test]
    public void Details_ExceptionThrown_ReturnsGenericErrorMessage()
    {
        // Arrange
        int productId = 1;
        string exceptionMessage = "Database error";
        _mockProductRepository.Setup(x => x.GetProductById(productId))
            .Throws(new Exception(exceptionMessage));

        // Act
        var result = _controller.Details(productId) as ViewResult;
        var model = result?.Model as ProductDetailsViewModel;

        // Assert
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Product, Is.Null);
        Assert.That(model.ErrorMessage, Does.Contain(exceptionMessage));
    }

    #endregion

    #region Manage Tests

    [Test]
    public void Manage_ReturnsAllProducts()
    {
        // Arrange
        var products = new List<Product>
        {
            new Product { ProductId = 1, ProductName = "Product 1" },
            new Product { ProductId = 2, ProductName = "Product 2" }
        };

        _mockProductRepository.Setup(x => x.GetAllProducts()).Returns(products);

        // Act
        var result = _controller.Manage() as ViewResult;
        var model = result?.Model as IEnumerable<Product>;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Count(), Is.EqualTo(2));
    }

    [Test]
    public void Manage_HasAuthorizeAttribute()
    {
        // Arrange
        var methodInfo = typeof(ProductController).GetMethod("Manage");

        // Act
        var attributes = methodInfo.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);

        // Assert
        Assert.That(attributes, Is.Not.Empty);
    }

    #endregion

    #region Add Tests (GET)

    [Test]
    public void Add_Get_ReturnsAddOrEditView()
    {
        // Arrange
        var categories = new List<Category> { new Category { CategoryId = 1 } };
        var suppliers = new List<Supplier> { new Supplier { SupplierId = 1 } };

        _mockCategoryRepository.Setup(x => x.GetAllCategories()).Returns(categories);
        _mockSupplierRepository.Setup(x => x.GetAllSuppliers()).Returns(suppliers);

        // Act
        var result = _controller.Add() as ViewResult;
        var model = result?.Model as ProductAddOrEditViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ViewName, Is.EqualTo("AddOrEdit"));
        Assert.That(model, Is.Not.Null);
        Assert.That(model.AddsNew, Is.True);
        Assert.That(model.ProductCategories, Is.EqualTo(categories));
        Assert.That(model.Suppliers, Is.EqualTo(suppliers));
    }

    #endregion

    #region Add Tests (POST)

    [Test]
    public void Add_Post_SuccessfulAdd_RedirectsToEdit()
    {
        // Arrange
        var product = new Product { ProductId = 0, ProductName = "New Product" };
        var savedProduct = new Product { ProductId = 10, ProductName = "New Product" };

        _mockProductRepository.Setup(x => x.Add(product)).Callback(() => product.ProductId = 10);

        // Act
        var result = _controller.Add(product) as RedirectToActionResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ActionName, Is.EqualTo("Edit"));
        Assert.That(result.RouteValues["id"], Is.EqualTo(10));
    }

    [Test]
    public void Add_Post_FailedAdd_ReturnsViewWithModel()
    {
        // Arrange
        var product = new Product { ProductId = 0, ProductName = "New Product" };
        var categories = new List<Category> { new Category { CategoryId = 1 } };
        var suppliers = new List<Supplier> { new Supplier { SupplierId = 1 } };

        _mockProductRepository.Setup(x => x.Add(product)).Throws<Exception>();
        _mockCategoryRepository.Setup(x => x.GetAllCategories()).Returns(categories);
        _mockSupplierRepository.Setup(x => x.GetAllSuppliers()).Returns(suppliers);

        // Act
        var result = _controller.Add(product) as ViewResult;
        var model = result?.Model as ProductAddOrEditViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ViewName, Is.EqualTo("AddOrEdit"));
        Assert.That(model, Is.Not.Null);
        Assert.That(model.AddsNew, Is.True);
        Assert.That(model.Product, Is.EqualTo(product));
    }

    #endregion

    #region Edit Tests (GET)

    [Test]
    public void Edit_Get_ReturnsProductForEditing()
    {
        // Arrange
        int productId = 1;
        var product = new Product { ProductId = productId, ProductName = "Test Product" };
        var categories = new List<Category> { new Category { CategoryId = 1 } };

        _mockProductRepository.Setup(x => x.GetProductById(productId)).Returns(product);
        _mockCategoryRepository.Setup(x => x.GetAllCategories()).Returns(categories);

        // Act
        var result = _controller.Edit(productId) as ViewResult;
        var model = result?.Model as ProductAddOrEditViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ViewName, Is.EqualTo("AddOrEdit"));
        Assert.That(model, Is.Not.Null);
        Assert.That(model.AddsNew, Is.False);
        Assert.That(model.Product, Is.EqualTo(product));
        Assert.That(model.ProductCategories, Is.EqualTo(categories));
    }

    #endregion

    #region Edit Tests (POST)

    [Test]
    public void Edit_Post_UpdatesProduct()
    {
        // Arrange
        var product = new Product { ProductId = 1, ProductName = "Updated Product" };
        var updatedProduct = new Product { ProductId = 1, ProductName = "Updated Product" };
        var categories = new List<Category> { new Category { CategoryId = 1 } };

        _mockProductRepository.Setup(x => x.Update(product)).Returns(updatedProduct);
        _mockCategoryRepository.Setup(x => x.GetAllCategories()).Returns(categories);

        // Act
        var result = _controller.Edit(product) as ViewResult;
        var model = result?.Model as ProductAddOrEditViewModel;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ViewName, Is.EqualTo("AddOrEdit"));
        Assert.That(model, Is.Not.Null);
        Assert.That(model.AddsNew, Is.False);
        Assert.That(model.Product, Is.EqualTo(updatedProduct));
        _mockProductRepository.Verify(x => x.Update(product), Times.Once);
    }

    #endregion

    #region Authorization Tests

    [Test]
    public void Add_Get_HasAuthorizeAttribute()
    {
        // Arrange
        var methodInfo = typeof(ProductController).GetMethod("Add", Type.EmptyTypes);

        // Act
        var attributes = methodInfo.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);
        var authorizeAttr = attributes.FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

        // Assert
        Assert.That(attributes, Is.Not.Empty);
        Assert.That(authorizeAttr?.Roles, Is.EqualTo("Admin"));
    }

    [Test]
    public void Add_Post_HasAuthorizeAttribute()
    {
        // Arrange
        var methodInfo = typeof(ProductController).GetMethod("Add", new[] { typeof(Product) });

        // Act
        var attributes = methodInfo.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);
        var authorizeAttr = attributes.FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

        // Assert
        Assert.That(attributes, Is.Not.Empty);
        Assert.That(authorizeAttr?.Roles, Is.EqualTo("Admin"));
    }

    [Test]
    public void Edit_Get_HasAuthorizeAttribute()
    {
        // Arrange
        var methodInfo = typeof(ProductController).GetMethod("Edit", new[] { typeof(int) });

        // Act
        var attributes = methodInfo.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);
        var authorizeAttr = attributes.FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

        // Assert
        Assert.That(attributes, Is.Not.Empty);
        Assert.That(authorizeAttr?.Roles, Is.EqualTo("Admin"));
    }

    [Test]
    public void Edit_Post_HasAuthorizeAttribute()
    {
        // Arrange
        var methodInfo = typeof(ProductController).GetMethod("Edit", new[] { typeof(Product) });

        // Act
        var attributes = methodInfo.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);
        var authorizeAttr = attributes.FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

        // Assert
        Assert.That(attributes, Is.Not.Empty);
        Assert.That(authorizeAttr?.Roles, Is.EqualTo("Admin"));
    }

    #endregion

    #region Helper Methods

    private void SetupFileProvider(string path, bool exists)
    {
        var mockFileInfo = new Mock<IFileInfo>();
        mockFileInfo.Setup(x => x.Exists).Returns(exists);
        _mockFileProvider.Setup(x => x.GetFileInfo(path)).Returns(mockFileInfo.Object);
    }

    #endregion
}