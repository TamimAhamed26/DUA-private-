using MDUA.Facade.Interface;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MDUA.Web.UI.Services
{
    public class SmartGeminiChatService : IAiChatService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private readonly IProductFacade _productFacade;
        private readonly IOrderFacade _orderFacade;
        private readonly IChatFacade _chatFacade;

        private const string ModelUrl ="https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";
        public SmartGeminiChatService(
            IConfiguration config,
            HttpClient httpClient,
            IProductFacade productFacade,
            IOrderFacade orderFacade,
            IChatFacade chatFacade)
        {
            _httpClient = httpClient;
            _productFacade = productFacade;
            _orderFacade = orderFacade;
            _chatFacade = chatFacade;

            // Inside SmartGeminiChatService constructor
            _apiKey = config["GEMINI_API_KEY"];

            // ADD THIS CLEANUP:
            if (!string.IsNullOrEmpty(_apiKey))
                _apiKey = _apiKey.Trim();

            if (string.IsNullOrEmpty(_apiKey))
                throw new Exception("Gemini API Key is missing.");
        }

        public async Task<string> GetResponseAsync(string userMessage, List<string> history)
        {
            var sb = new StringBuilder();

            // 🔥 SYSTEM PROMPT with Instructions
            sb.AppendLine(@"You are MDUA Assistant, a helpful AI for MDUA - an e-commerce platform in Bangladesh.

 CAPABILITIES:
✅ Search and recommend products
✅ Check stock availability
✅ Explain prices and discounts
✅ Track orders by Order ID
✅ Guide customers through checkout
✅ Answer delivery questions (Inside Dhaka: ৳60, Outside: ৳120)

IMPORTANT RULES:
1. Be friendly, concise, and use emojis occasionally 😊
2. When mentioning products, always include price in BDT (৳)
3. If stock is 0, say 'Currently out of stock'
4. If you need human help, say: 'Let me connect you with our support team for personalized assistance.'
5. For order tracking, ask for the Order ID (format: ONXXXXXXXX or DOXXXXXXXX)
6. Always format prices as: ৳1,500 (with commas)

RESPONSE STYLE:
- Keep answers under 3 sentences when possible
- Use bullet points for product lists
- Be conversational, not robotic
");

            // 🆕 DYNAMIC CONTEXT INJECTION
            string contextData = await GetRelevantContext(userMessage);
            if (!string.IsNullOrEmpty(contextData))
            {
                sb.AppendLine("\n--- REAL-TIME DATA FROM DATABASE ---");
                sb.AppendLine(contextData);
                sb.AppendLine("--- END DATA ---\n");
            }

            // Add conversation history
            if (history != null && history.Count > 0)
            {
                sb.AppendLine("Conversation history:");
                foreach (var line in history.Take(5)) // Last 5 messages only
                {
                    sb.AppendLine(line);
                }
            }

            sb.AppendLine($"\nCustomer: {userMessage}");
            sb.AppendLine("AI:");

            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = sb.ToString() } } } }
            };

            var jsonContent = new StringContent(
                JsonConvert.SerializeObject(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync($"{ModelUrl}?key={_apiKey}", jsonContent);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                dynamic jsonResponse = JsonConvert.DeserializeObject(responseString);
                string aiText = jsonResponse?.candidates?[0]?.content?.parts?[0]?.text;

                // 🔍 Check if AI is requesting human help
                if (ContainsHandoffTrigger(aiText))
                {
                    return aiText + "\n\n🔔 *I've notified our team. A support agent will join shortly.*";
                }

                return aiText ?? "I'm sorry, I couldn't generate a response.";
            }

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return $"Gemini error {(int)response.StatusCode}: {err}";
            }

            return $"AI is currently unavailable. Please try again or contact support.";
        }

        // 🧠 INTELLIGENCE ENGINE: Detects intent and fetches relevant data
        private async Task<string> GetRelevantContext(string message)
        {
            var lowerMsg = message.ToLower();
            var context = new StringBuilder();

            try
            {
                // 1️⃣ PRODUCT SEARCH
                if (ContainsAny(lowerMsg, "product", "item", "buy", "purchase", "show", "find", "available", "stock", "price"))
                {
                    var productInfo = await GetProductContext(message);
                    if (!string.IsNullOrEmpty(productInfo))
                        context.AppendLine(productInfo);
                }

                // 2️⃣ ORDER TRACKING
                if (ContainsAny(lowerMsg, "order", "track", "delivery", "shipped", "on", "do") &&
                    (lowerMsg.Contains("on") || lowerMsg.Contains("do")))
                {
                    var orderInfo = await GetOrderContext(message);
                    if (!string.IsNullOrEmpty(orderInfo))
                        context.AppendLine(orderInfo);
                }

                // 3️⃣ LOW STOCK ALERTS (for recommendations)
                if (ContainsAny(lowerMsg, "recommend", "suggest", "popular", "trending", "best"))
                {
                    var trending = GetTrendingProducts();
                    if (!string.IsNullOrEmpty(trending))
                        context.AppendLine(trending);
                }
            }
            catch (Exception ex)
            {
                context.AppendLine($"Note: Some data couldn't be retrieved ({ex.Message})");
            }

            return context.ToString();
        }

        // 📦 PRODUCT CONTEXT BUILDER
        private async Task<string> GetProductContext(string query)
        {
            try
            {
                // Extract search keywords
                var searchTerm = ExtractSearchTerm(query);
                var products = _productFacade.SearchProducts(searchTerm);

                if (products == null || products.Count == 0)
                    return $"❌ No products found matching '{searchTerm}'";

                var sb = new StringBuilder();
                sb.AppendLine($"📦 **Products matching '{searchTerm}':**\n");

                int count = 0;
                foreach (var p in products.Take(5)) // Limit to 5 results
                {
                    count++;

                    // Get variant stock info
                    var variants = _productFacade.GetVariantsByProductId(p.Id);
                    int totalStock = variants?.Sum(v => v.StockQty) ?? 0;

                    string stockStatus = totalStock > 0 ? $"✅ In Stock ({totalStock} available)" : "❌ Out of Stock";

                    // Get best discount
                    var discount = _productFacade.GetBestDiscount(p.Id, p.BasePrice ?? 0);
                    string priceDisplay = discount != null
                        ? $"৳{p.SellingPrice:N0} (was ৳{p.BasePrice:N0})"
                        : $"৳{p.BasePrice:N0}";

                    sb.AppendLine($"{count}. **{p.ProductName}**");
                    sb.AppendLine($"   Price: {priceDisplay}");
                    sb.AppendLine($"   Stock: {stockStatus}");

                    if (!string.IsNullOrEmpty(p.Description))
                    {
                        var desc = p.Description.Length > 100
                            ? p.Description.Substring(0, 100) + "..."
                            : p.Description;

                        sb.AppendLine($"   Description: {desc}");
                    }

                    sb.AppendLine();
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error fetching products: {ex.Message}";
            }
        }

        // 📋 ORDER TRACKING CONTEXT
        private async Task<string> GetOrderContext(string message)
        {
            try
            {
                // Extract Order ID (format: ON12345678 or DO12345678)
                var orderIdMatch = System.Text.RegularExpressions.Regex.Match(
                    message,
                    @"(ON|DO)\d{8}",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (!orderIdMatch.Success)
                    return "💡 To track your order, please provide your Order ID (e.g., ON12345678 or DO12345678)";

                string orderId = orderIdMatch.Value.ToUpper();

                // Fetch order details
                var orderDetails = _orderFacade.GetOrderReceiptByOnlineId(orderId);

                if (orderDetails == null || orderDetails.Count == 0)
                    return $"❌ Order {orderId} not found. Please verify the Order ID.";

                var order = orderDetails[0] as dynamic;

                var sb = new StringBuilder();
                sb.AppendLine($"📦 **Order {orderId} Status:**\n");
                sb.AppendLine($"Status: {order.Status}");
                sb.AppendLine($"Order Date: {Convert.ToDateTime(order.OrderDate):dd MMM yyyy}");
                sb.AppendLine($"Total Amount: ৳{order.TotalAmount:N0}");

                if (order.Status == "Shipped" || order.Status == "Delivered")
                    sb.AppendLine($"Delivery: Expected in 2-5 business days");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error tracking order: {ex.Message}";
            }
        }

        // 🔥 TRENDING PRODUCTS
        private string GetTrendingProducts()
        {
            try
            {
                var lowStock = _productFacade.GetLowStockVariants(5);

                if (lowStock == null || lowStock.Count == 0)
                    return "";

                var sb = new StringBuilder();
                sb.AppendLine("🔥 **Trending/Low Stock Items (Grab them fast!):**\n");

                foreach (var item in lowStock)
                {
                    sb.AppendLine($"• {item.ProductName} - ৳{item.Price:N0} (Only {item.StockQty} left!)");
                }

                return sb.ToString();
            }
            catch
            {
                return "";
            }
        }

        // 🔍 HELPER: Extract search keywords
        private string ExtractSearchTerm(string message)
        {
            var stopWords = new[] { "show", "me", "find", "search", "for", "the", "a", "an", "want", "need", "looking", "any" };
            var words = message.ToLower()
                              .Split(new[] { ' ', ',', '?', '!' }, StringSplitOptions.RemoveEmptyEntries)
                              .Where(w => !stopWords.Contains(w) && w.Length > 2);

            return string.Join(" ", words);
        }

        // 🔍 HELPER: Check if message contains any keyword
        private bool ContainsAny(string text, params string[] keywords)
        {
            return keywords.Any(k => text.Contains(k));
        }

        // 🚨 HELPER: Detect if AI wants human takeover
        private bool ContainsHandoffTrigger(string aiResponse)
        {
            if (string.IsNullOrEmpty(aiResponse)) return false;

            var triggers = new[] {
                "support team",
                "human agent",
                "connect you",
                "speak with someone",
                "can't help with that",
                "beyond my capability"
            };

            return triggers.Any(t => aiResponse.ToLower().Contains(t));
        }
    }
}