using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Common.Security;
using Wealthra.Application.Features.Categories.Commands.CreateCategory;
using Wealthra.Application.Features.Categories.Commands.DeleteCategory;
using Wealthra.Application.Features.Categories.Commands.UpdateCategory;
using Wealthra.Application.Features.Categories.Models;
using Wealthra.Application.Features.Categories.Queries.GetAllCategories;

namespace Wealthra.Api.Controllers;

public class CategoriesController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetAll([FromQuery] string language = "en")
    {
        if (!TryParseCategoryLanguage(language, out var lang))
        {
            return BadRequest("Invalid language. Use 'en' or 'tr'.");
        }

        var categories = await Mediator.Send(new GetAllCategoriesQuery(lang));
        return Ok(categories);
    }

    private static bool TryParseCategoryLanguage(string? value, out CategoryDisplayLanguage lang)
    {
        lang = CategoryDisplayLanguage.English;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "en":
                return true;
            case "tr":
                lang = CategoryDisplayLanguage.Turkish;
                return true;
            default:
                return false;
        }
    }

    [Authorize(Policy = AuthPolicies.AdminElevated)]
    [HttpPost]
    public async Task<ActionResult<int>> Create(CreateCategoryCommand command)
    {
        var categoryId = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id = categoryId }, categoryId);
    }

    [Authorize(Policy = AuthPolicies.AdminElevated)]
    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, UpdateCategoryCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest("ID mismatch");
        }

        await Mediator.Send(command);
        return NoContent();
    }

    [Authorize(Policy = AuthPolicies.AdminElevated)]
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteCategoryCommand(id));
        return NoContent();
    }
}
