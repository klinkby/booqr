using System.ComponentModel.DataAnnotations;
using Klinkby.Booqr.Application.Models;

namespace Klinkby.Booqr.Application.Tests.Models;

public class ByIdRequestTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Constructor_SetsId(int id)
    {
        var request = new ByIdRequest(id);
        Assert.Equal(id, request.Id);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void ImplicitOperator_ConvertsFromInt(int id)
    {
        ByIdRequest request = id;
        Assert.Equal(id, request.Id);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    public void FromInt32_CreatesRequest(int id)
    {
        var request = ByIdRequest.FromInt32(id);
        Assert.Equal(id, request.Id);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void Id_Validation_Range(int id, bool expectedValid)
    {
        var request = new ByIdRequest(id);
        object boxed = request;
        var context = new ValidationContext(boxed);
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(boxed, context, results, true);

        Assert.Equal(expectedValid, isValid);
        if (!expectedValid)
        {
            Assert.Contains(results, r => r.MemberNames.Contains("Id"));
        }
    }
}
