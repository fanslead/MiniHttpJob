using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MiniHttpJob.Shared.Validators;
using MiniHttpJob.Shared.DTOs;
using FluentValidation;

namespace MiniHttpJob.Tests;

public class ValidatorTests
{
    private readonly CreateJobDtoValidator _createJobValidator;
    private readonly UpdateJobDtoValidator _updateJobValidator;

    public ValidatorTests()
    {
        _createJobValidator = new CreateJobDtoValidator();
        _updateJobValidator = new UpdateJobDtoValidator();
    }

    [Fact]
    public void CreateJobDtoValidator_WithValidData_PassesValidation()
    {
        // Arrange
        var dto = new CreateJobDto
        {
            Name = "Valid Job",
            CronExpression = "0/30 * * * * ?",
            HttpMethod = "GET",
            Url = "https://api.example.com/test",
            Headers = "{}",
            Body = ""
        };

        // Act
        var result = _createJobValidator.Validate(dto);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void CreateJobDtoValidator_WithInvalidName_FailsValidation(string invalidName)
    {
        // Arrange
        var dto = new CreateJobDto
        {
            Name = invalidName,
            CronExpression = "0/30 * * * * ?",
            HttpMethod = "GET",
            Url = "https://api.example.com/test"
        };

        // Act
        var result = _createJobValidator.Validate(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(dto.Name));
    }

    [Fact]
    public void CreateJobDtoValidator_WithTooLongName_FailsValidation()
    {
        // Arrange
        var dto = new CreateJobDto
        {
            Name = new string('A', 101), // Exceeds 100 character limit
            CronExpression = "0/30 * * * * ?",
            HttpMethod = "GET",
            Url = "https://api.example.com/test"
        };

        // Act
        var result = _createJobValidator.Validate(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(dto.Name));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("invalid-cron")]
    public void CreateJobDtoValidator_WithInvalidCronExpression_FailsValidation(string invalidCron)
    {
        // Arrange
        var dto = new CreateJobDto
        {
            Name = "Valid Job",
            CronExpression = invalidCron,
            HttpMethod = "GET",
            Url = "https://api.example.com/test"
        };

        // Act
        var result = _createJobValidator.Validate(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(dto.CronExpression));
    }

    [Theory]
    [InlineData("INVALID")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("get")] // lowercase
    public void CreateJobDtoValidator_WithInvalidHttpMethod_FailsValidation(string invalidMethod)
    {
        // Arrange
        var dto = new CreateJobDto
        {
            Name = "Valid Job",
            CronExpression = "0/30 * * * * ?",
            HttpMethod = invalidMethod,
            Url = "https://api.example.com/test"
        };

        // Act
        var result = _createJobValidator.Validate(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(dto.HttpMethod));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public void CreateJobDtoValidator_WithValidHttpMethods_PassesValidation(string validMethod)
    {
        // Arrange
        var dto = new CreateJobDto
        {
            Name = "Valid Job",
            CronExpression = "0/30 * * * * ?",
            HttpMethod = validMethod,
            Url = "https://api.example.com/test"
        };

        // Act
        var result = _createJobValidator.Validate(dto);

        // Assert
        Assert.True(result.IsValid || !result.Errors.Any(e => e.PropertyName == nameof(dto.HttpMethod)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("invalid-url")]
    [InlineData("http://")]
    [InlineData("not-a-url")]
    public void CreateJobDtoValidator_WithInvalidUrl_FailsValidation(string invalidUrl)
    {
        // Arrange
        var dto = new CreateJobDto
        {
            Name = "Valid Job",
            CronExpression = "0/30 * * * * ?",
            HttpMethod = "GET",
            Url = invalidUrl
        };

        // Act
        var result = _createJobValidator.Validate(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(dto.Url));
    }

    [Theory]
    [InlineData("https://api.example.com/test")]
    [InlineData("http://localhost:8080/api")]
    [InlineData("https://subdomain.example.com/path/to/resource")]
    public void CreateJobDtoValidator_WithValidUrl_PassesValidation(string validUrl)
    {
        // Arrange
        var dto = new CreateJobDto
        {
            Name = "Valid Job",
            CronExpression = "0/30 * * * * ?",
            HttpMethod = "GET",
            Url = validUrl
        };

        // Act
        var result = _createJobValidator.Validate(dto);

        // Assert
        Assert.True(result.IsValid || !result.Errors.Any(e => e.PropertyName == nameof(dto.Url)));
    }

    [Fact]
    public void UpdateJobDtoValidator_WithValidData_PassesValidation()
    {
        // Arrange
        var dto = new UpdateJobDto
        {
            Name = "Updated Job",
            CronExpression = "0/60 * * * * ?",
            HttpMethod = "POST",
            Url = "https://api.example.com/updated",
            Headers = "{\"Content-Type\": \"application/json\"}",
            Body = "{\"updated\": true}"
        };

        // Act
        var result = _updateJobValidator.Validate(dto);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateJobDtoValidator_WithEmptyHeaders_UsesDefaultValue()
    {
        // Arrange
        var dto = new UpdateJobDto
        {
            Name = "Valid Job",
            CronExpression = "0/30 * * * * ?",
            HttpMethod = "GET",
            Url = "https://api.example.com/test",
            Headers = "", // Empty headers should be allowed
            Body = ""
        };

        // Act
        var result = _updateJobValidator.Validate(dto);

        // Assert
        Assert.True(result.IsValid);
    }
}