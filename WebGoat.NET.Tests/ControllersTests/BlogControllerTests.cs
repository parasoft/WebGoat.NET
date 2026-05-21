using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using WebGoatCore.Controllers;
using WebGoatCore.Data;
using WebGoatCore.Models;

namespace WebGoat.NET.Tests.Controllers;

[TestFixture]
public class BlogControllerTests
{
    private Mock<BlogEntryRepository> _mockBlogEntryRepository;
    private Mock<BlogResponseRepository> _mockBlogResponseRepository;
    private Mock<NorthwindContext> _mockContext;
    private BlogController _controller;
    private const string TestUsername = "testuser";
    private const string TestAdminUsername = "adminuser";

    [SetUp]
    public void Setup()
    {
        _mockBlogEntryRepository = new Mock<BlogEntryRepository>();
        _mockBlogResponseRepository = new Mock<BlogResponseRepository>();
        _mockContext = new Mock<NorthwindContext>();

        _controller = new BlogController(
            _mockBlogEntryRepository.Object,
            _mockBlogResponseRepository.Object,
            _mockContext.Object
        );

        // Setup HttpContext with authenticated user
        SetupHttpContextWithUser(TestUsername, "User");
    }

    private void SetupHttpContextWithUser(string username, string role)
    {
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);
        httpContext.User = user;

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    #region Index Tests

    [Test]
    public void Index_ReturnsViewWithBlogEntries()
    {
        // Arrange
        var blogEntries = CreateTestBlogEntries();
        _mockBlogEntryRepository.Setup(x => x.GetTopBlogEntries())
            .Returns(blogEntries);

        // Act
        var result = _controller.Index() as ViewResult;
        var model = result?.Model as List<BlogEntry>;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Count, Is.EqualTo(3));
        Assert.That(model[0].Title, Is.EqualTo("Blog Entry 1"));
    }

    [Test]
    public void Index_CallsGetTopBlogEntries()
    {
        // Arrange
        _mockBlogEntryRepository.Setup(x => x.GetTopBlogEntries())
            .Returns(new List<BlogEntry>());

        // Act
        _controller.Index();

        // Assert
        _mockBlogEntryRepository.Verify(x => x.GetTopBlogEntries(), Times.Once);
    }

    [Test]
    public void Index_WithEmptyBlogList_ReturnsEmptyList()
    {
        // Arrange
        _mockBlogEntryRepository.Setup(x => x.GetTopBlogEntries())
            .Returns(new List<BlogEntry>());

        // Act
        var result = _controller.Index() as ViewResult;
        var model = result?.Model as List<BlogEntry>;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Count, Is.EqualTo(0));
    }

    #endregion

    #region Reply GET Tests

    [Test]
    public void Reply_Get_WithValidEntryId_ReturnsViewWithBlogEntry()
    {
        // Arrange
        int entryId = 1;
        var blogEntry = CreateTestBlogEntry(entryId);
        _mockBlogEntryRepository.Setup(x => x.GetBlogEntry(entryId))
            .Returns(blogEntry);

        // Act
        var result = _controller.Reply(entryId) as ViewResult;
        var model = result?.Model as BlogEntry;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Id, Is.EqualTo(entryId));
        Assert.That(model.Title, Is.EqualTo("Test Blog Entry"));
    }

    [Test]
    public void Reply_Get_CallsGetBlogEntryWithCorrectId()
    {
        // Arrange
        int entryId = 42;
        var blogEntry = CreateTestBlogEntry(entryId);
        _mockBlogEntryRepository.Setup(x => x.GetBlogEntry(entryId))
            .Returns(blogEntry);

        // Act
        _controller.Reply(entryId);

        // Assert
        _mockBlogEntryRepository.Verify(x => x.GetBlogEntry(entryId), Times.Once);
    }

    #endregion

    #region Reply POST Tests

    [Test]
    public void Reply_Post_WithAuthenticatedUser_CreatesBlogResponse()
    {
        // Arrange
        int entryId = 1;
        string contents = "This is my reply";

        // Act
        var result = _controller.Reply(entryId, contents) as RedirectToActionResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ActionName, Is.EqualTo("Index"));
        _mockBlogResponseRepository.Verify(x => x.CreateBlogResponse(
            It.Is<BlogResponse>(r =>
                r.BlogEntryId == entryId &&
                r.Contents == contents &&
                r.Author == TestUsername &&
                r.ResponseDate <= DateTime.Now &&
                r.ResponseDate >= DateTime.Now.AddSeconds(-5)
            )), Times.Once);
    }

    [Test]
    public void Reply_Post_WithAnonymousUser_UsesAnonymousAuthor()
    {
        // Arrange
        int entryId = 1;
        string contents = "Anonymous reply";
        
        // Setup anonymous user
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = _controller.Reply(entryId, contents);

        // Assert
        _mockBlogResponseRepository.Verify(x => x.CreateBlogResponse(
            It.Is<BlogResponse>(r => r.Author == "Anonymous")), Times.Once);
    }

    [Test]
    public void Reply_Post_WithNullIdentity_UsesAnonymousAuthor()
    {
        // Arrange
        int entryId = 1;
        string contents = "Reply with null identity";
        
        // Setup user with null identity
        var httpContext = new DefaultHttpContext();
        httpContext.User = null;
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = _controller.Reply(entryId, contents);

        // Assert
        _mockBlogResponseRepository.Verify(x => x.CreateBlogResponse(
            It.Is<BlogResponse>(r => r.Author == "Anonymous")), Times.Once);
    }

    [Test]
    public void Reply_Post_SetsCorrectResponseDate()
    {
        // Arrange
        int entryId = 1;
        string contents = "Time test reply";
        var beforeTime = DateTime.Now;

        // Act
        _controller.Reply(entryId, contents);
        var afterTime = DateTime.Now;

        // Assert
        _mockBlogResponseRepository.Verify(x => x.CreateBlogResponse(
            It.Is<BlogResponse>(r =>
                r.ResponseDate >= beforeTime &&
                r.ResponseDate <= afterTime
            )), Times.Once);
    }

    [Test]
    public void Reply_Post_WithEmptyContents_CreatesResponseWithEmptyContents()
    {
        // Arrange
        int entryId = 1;
        string contents = "";

        // Act
        var result = _controller.Reply(entryId, contents);

        // Assert
        _mockBlogResponseRepository.Verify(x => x.CreateBlogResponse(
            It.Is<BlogResponse>(r => r.Contents == "")), Times.Once);
    }

    [Test]
    public void Reply_Post_WithLongContents_CreatesResponseWithFullContents()
    {
        // Arrange
        int entryId = 1;
        string contents = new string('x', 5000);

        // Act
        var result = _controller.Reply(entryId, contents);

        // Assert
        _mockBlogResponseRepository.Verify(x => x.CreateBlogResponse(
            It.Is<BlogResponse>(r => r.Contents == contents)), Times.Once);
    }

    #endregion

    #region Create GET Tests

    [Test]
    public void Create_Get_WithAdminUser_ReturnsView()
    {
        // Arrange
        SetupHttpContextWithUser(TestAdminUsername, "Admin");

        // Act
        var result = _controller.Create() as ViewResult;

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    #endregion

    #region Create POST Tests

    [Test]
    public void Create_Post_WithValidData_CreatesBlogEntry()
    {
        // Arrange
        SetupHttpContextWithUser(TestAdminUsername, "Admin");
        string title = "New Blog Post";
        string contents = "This is the blog content";
        var createdEntry = new BlogEntry
        {
            Id = 1,
            Title = title,
            Contents = contents,
            Author = TestAdminUsername,
            PostedDate = DateTime.Now
        };

        _mockBlogEntryRepository.Setup(x => x.CreateBlogEntry(title, contents, TestAdminUsername))
            .Returns(createdEntry);

        // Act
        var result = _controller.Create(title, contents) as ViewResult;
        var model = result?.Model as BlogEntry;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Title, Is.EqualTo(title));
        Assert.That(model.Contents, Is.EqualTo(contents));
        Assert.That(model.Author, Is.EqualTo(TestAdminUsername));
        _mockBlogEntryRepository.Verify(x => x.CreateBlogEntry(title, contents, TestAdminUsername), Times.Once);
    }

    [Test]
    public void Create_Post_CallsCreateBlogEntryWithCorrectParameters()
    {
        // Arrange
        SetupHttpContextWithUser(TestAdminUsername, "Admin");
        string title = "Test Title";
        string contents = "Test Contents";
        var createdEntry = CreateTestBlogEntry(1);

        _mockBlogEntryRepository.Setup(x => x.CreateBlogEntry(title, contents, TestAdminUsername))
            .Returns(createdEntry);

        // Act
        _controller.Create(title, contents);

        // Assert
        _mockBlogEntryRepository.Verify(x => x.CreateBlogEntry(
            title,
            contents,
            TestAdminUsername), Times.Once);
    }

    [Test]
    public void Create_Post_WithEmptyTitle_CallsRepository()
    {
        // Arrange
        SetupHttpContextWithUser(TestAdminUsername, "Admin");
        string title = "";
        string contents = "Contents";
        var createdEntry = CreateTestBlogEntry(1);

        _mockBlogEntryRepository.Setup(x => x.CreateBlogEntry(title, contents, TestAdminUsername))
            .Returns(createdEntry);

        // Act
        _controller.Create(title, contents);

        // Assert
        _mockBlogEntryRepository.Verify(x => x.CreateBlogEntry(title, contents, TestAdminUsername), Times.Once);
    }

    [Test]
    public void Create_Post_WithEmptyContents_CallsRepository()
    {
        // Arrange
        SetupHttpContextWithUser(TestAdminUsername, "Admin");
        string title = "Title";
        string contents = "";
        var createdEntry = CreateTestBlogEntry(1);

        _mockBlogEntryRepository.Setup(x => x.CreateBlogEntry(title, contents, TestAdminUsername))
            .Returns(createdEntry);

        // Act
        _controller.Create(title, contents);

        // Assert
        _mockBlogEntryRepository.Verify(x => x.CreateBlogEntry(title, contents, TestAdminUsername), Times.Once);
    }

    [Test]
    public void Create_Post_ReturnsViewWithCreatedEntry()
    {
        // Arrange
        SetupHttpContextWithUser(TestAdminUsername, "Admin");
        string title = "New Entry";
        string contents = "New Contents";
        var createdEntry = new BlogEntry
        {
            Id = 99,
            Title = title,
            Contents = contents,
            Author = TestAdminUsername,
            PostedDate = DateTime.Now
        };

        _mockBlogEntryRepository.Setup(x => x.CreateBlogEntry(title, contents, TestAdminUsername))
            .Returns(createdEntry);

        // Act
        var result = _controller.Create(title, contents) as ViewResult;
        var model = result?.Model as BlogEntry;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Id, Is.EqualTo(99));
    }

    #endregion

    #region Helper Methods

    private BlogEntry CreateTestBlogEntry(int id)
    {
        return new BlogEntry
        {
            Id = id,
            Title = "Test Blog Entry",
            Contents = "This is test content",
            Author = "TestAuthor",
            PostedDate = DateTime.Now.AddDays(-1)
        };
    }

    private List<BlogEntry> CreateTestBlogEntries()
    {
        return new List<BlogEntry>
        {
            new BlogEntry
            {
                Id = 1,
                Title = "Blog Entry 1",
                Contents = "Contents 1",
                Author = "Author1",
                PostedDate = DateTime.Now.AddDays(-1)
            },
            new BlogEntry
            {
                Id = 2,
                Title = "Blog Entry 2",
                Contents = "Contents 2",
                Author = "Author2",
                PostedDate = DateTime.Now.AddDays(-2)
            },
            new BlogEntry
            {
                Id = 3,
                Title = "Blog Entry 3",
                Contents = "Contents 3",
                Author = "Author3",
                PostedDate = DateTime.Now.AddDays(-3)
            }
        };
    }

    #endregion
}
