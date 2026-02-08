using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Features.Categories.Commands.CreateCategory;
using Wealthra.Application.Features.Categories.Commands.DeleteCategory;
using Wealthra.Application.Features.Categories.Commands.UpdateCategory;
using Wealthra.Application.Features.Categories.Models;
using Wealthra.Application.Features.Categories.Queries.GetAllCategories;

namespace Wealthra.Api.Controllers;

public class CategoriesController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetAll()
    {
        var categories = await Mediator.Send(new GetAllCategoriesQuery());
        return Ok(categories);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<int>> Create(CreateCategoryCommand command)
    {
        var categoryId = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id = categoryId }, categoryId);
    }

    [Authorize(Roles = "Admin")]
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

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteCategoryCommand(id));
        return NoContent();
    }
}
