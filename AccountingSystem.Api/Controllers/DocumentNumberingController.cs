using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSystem.API.Controllers
{
    [ApiController]
    [Route("api/document-numbering")]
    [Authorize(Roles = "Admin")]
    public class DocumentNumberingController : ControllerBase
    {
        private readonly IDocumentSequenceService _documentSequenceService;
        private readonly ITenantService _tenantService;

        public DocumentNumberingController(IDocumentSequenceService documentSequenceService, ITenantService tenantService)
        {
            _documentSequenceService = documentSequenceService;
            _tenantService = tenantService;
        }

        [HttpGet]
        public async Task<IActionResult> GetSequences()
        {
            var data = await _documentSequenceService.GetSequencesAsync(_tenantService.GetCurrentTenant());
            return Ok(data);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateSequence([FromBody] UpdateDocumentSequenceDTO dto)
        {
            var updated = await _documentSequenceService.UpsertSequenceAsync(_tenantService.GetCurrentTenant(), dto);
            return Ok(updated);
        }
    }
}
