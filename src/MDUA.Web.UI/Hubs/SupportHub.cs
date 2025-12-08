using Microsoft.AspNetCore.SignalR;
using MDUA.Entities;
using MDUA.Facade.Interface;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MDUA.Web.UI.Hubs
{
    public class SupportHub : Hub
    {
        private readonly IChatFacade _chatFacade;

        public SupportHub(IChatFacade chatFacade)
        {
            _chatFacade = chatFacade;
        }

        // 1. GUEST SENDS MESSAGE
        public async Task SendMessageToAdmin(string user, string message, string sessionGuidString)
        {
            if (!Guid.TryParse(sessionGuidString, out Guid sessionGuid)) return;
            string groupName = sessionGuidString.ToLower();

            // Init session (also handles name update in facade if implemented)
            var session = _chatFacade.InitGuestSession(sessionGuid);

            var chatMsg = new ChatMessage
            {
                ChatSessionId = session.Id,
                SenderName = user,
                MessageText = message,
                IsFromAdmin = false,
                SentAt = DateTime.Now
            };
            _chatFacade.SendMessage(chatMsg);

            await Clients.Group("Admins").SendAsync("ReceiveMessage", user, message, groupName);
        }

        // 2. ADMIN JOINS A SESSION (Notification)
        public async Task AdminJoinSession(string adminName, string targetSessionGuidString)
        {
            if (string.IsNullOrEmpty(targetSessionGuidString)) return;
            string targetGroup = targetSessionGuidString.ToLower();

            // Notify Guest
            await Clients.Group(targetGroup).SendAsync("ReceiveSystemMessage", $"{adminName} has joined the chat.");
        }

        // 3. ADMIN SENDS REPLY
        public async Task SendReplyToUser(string adminName, string message, string targetSessionGuidString)
        {
            // Simple check: Is Authenticated?
            if (!Context.GetHttpContext().User.Identity.IsAuthenticated)
                throw new HubException("Unauthorized");

            if (!Guid.TryParse(targetSessionGuidString, out Guid sessionGuid)) return;
            string targetGroup = targetSessionGuidString.ToLower();

            var session = _chatFacade.InitGuestSession(sessionGuid);
            var chatMsg = new ChatMessage
            {
                ChatSessionId = session.Id,
                SenderName = adminName,
                MessageText = message,
                IsFromAdmin = true,
                SentAt = DateTime.Now
            };
            _chatFacade.SendMessage(chatMsg);

            await Clients.Group(targetGroup).SendAsync("ReceiveReply", adminName, message);
        }

        public override async Task OnConnectedAsync()
        {
            var user = Context.GetHttpContext().User;
            // Admin Group Logic
            if (user.Identity.IsAuthenticated && (user.HasClaim(c => c.Value == "Chat.View") || user.HasClaim(c => c.Value == "Order.View")))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
            }
            // Guest Group Logic
            else
            {
                string sessionId = Context.GetHttpContext().Request.Query["sessionId"];
                if (!string.IsNullOrEmpty(sessionId))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, sessionId.ToLower());
                }
            }
            await base.OnConnectedAsync();
        }
    }
}