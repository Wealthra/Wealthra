using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Features.Ocr.Commands.ExtractText;

namespace Wealthra.Api.Controllers
{
    [Authorize]
    public class OcrController : ApiControllerBase
    {
        /// <summary>
        /// Extracts text from an uploaded image using Tesseract OCR.
        /// </summary>
        [HttpPost("extract-text")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024, ValueCountLimit = int.MaxValue)]
        public async Task<ActionResult<ExtractTextResponse>> ExtractText([FromForm] ExtractTextCommand command)
        {
            var result = await Mediator.Send(command);
            return Ok(result);
        }
    }
}
