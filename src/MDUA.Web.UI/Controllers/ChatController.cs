using MDUA.Entities;
using MDUA.Facade.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MDUA.Web.UI.Controllers
{
    [Authorize]
    public class ChatController : BaseController
    {
        private readonly IChatFacade _chatFacade;

        public ChatController(IChatFacade chatFacade)
        {
            _chatFacade = chatFacade;
        }

        [HttpGet]
        [Route("chat/active-sessions")]
        public IActionResult GetActiveSessions()
        {
            // ✅ FIX: Use new permission
            if (!HasPermission("Chat.View")) return Unauthorized();

            var sessions = _chatFacade.GetActiveSessionsForAdmin();
            return Json(sessions);
        }

        [HttpGet]
        [Route("chat/history")]
        public IActionResult GetHistory(int sessionId)
        {
            if (!HasPermission("Chat.View")) return Unauthorized();

            var history = _chatFacade.GetChatHistory(sessionId);

            // Mark as read
            bool isAdmin = HasPermission("Chat.View");
            _chatFacade.MarkMessagesAsRead(sessionId, isAdmin);

            return Json(history);
        }
    }
}