using MDUA.Facade;
using MDUA.Facade.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace MDUA.Web.UI.Controllers
{
    [Authorize]
    public class SettingsController : BaseController
    {
        private readonly ISettingsFacade _settingsFacade;
        private  readonly IPaymentFacade _paymentFacade;
        private readonly IUserLoginFacade _userLoginFacade;
        public SettingsController(ISettingsFacade settingsFacade, IPaymentFacade paymentFacade, IUserLoginFacade userLoginFacade)
        {
            _settingsFacade = settingsFacade;
            _paymentFacade = paymentFacade;
            _userLoginFacade = userLoginFacade;

        }

        [HttpGet]
        public IActionResult PaymentSettings()
        {
            var model = _settingsFacade.GetCompanyPaymentSettings(CurrentCompanyId);

            // Pass delivery charges via ViewBag or extend your ViewModel
            var delivery = _settingsFacade.GetDeliverySettings(CurrentCompanyId);
            ViewBag.DeliveryDhaka = delivery["dhaka"];
            ViewBag.DeliveryOutside = delivery["outside"];

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SavePaymentConfig(int methodId, bool isEnabled, bool isManual, bool isGateway, string instruction)
        {
            try
            {
                _settingsFacade.SavePaymentConfig(
                    CurrentCompanyId,
                    methodId,
                    isEnabled,
                    isManual,
                    isGateway,
                    instruction,
                    CurrentUserName
                );
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

      

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveDeliverySettings(int dhakaCharge, int outsideCharge)
        {
            try
            {
                _settingsFacade.SaveDeliverySettings(CurrentCompanyId, dhakaCharge, outsideCharge);
                return Json(new { success = true, message = "Delivery charges updated!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ✅ NEW: Security Settings Page
        [HttpGet]
        public IActionResult Security()
        {
            // Check if user already has 2FA enabled
            var result = _userLoginFacade.GetUserLoginById(CurrentUserId);
            bool isEnabled = result.IsSuccess && result.UserLogin.IsTwoFactorEnabled;

            if (isEnabled)
            {
                ViewBag.IsTwoFactorEnabled = true;
                return View();
            }

            // Generate Setup Data
            var setupData = _userLoginFacade.SetupTwoFactor(CurrentUserName);

            // Generate QR Code
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(setupData.qrCodeUri, QRCodeGenerator.ECCLevel.Q))
            using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
            {
                byte[] qrCodeBytes = qrCode.GetGraphic(20);
                string base64Qr = Convert.ToBase64String(qrCodeBytes);
                ViewBag.QrCodeImage = $"data:image/png;base64,{base64Qr}";
                ViewBag.ManualEntryKey = setupData.secretKey;
            }

            return View();
        }

        // ✅ NEW: Enable 2FA Action
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EnableTwoFactor(string entryKey, string code)
        {
            bool success = _userLoginFacade.EnableTwoFactor(CurrentUserId, entryKey, code);

            if (success)
            {
                return Json(new { success = true, message = "Two-Factor Authentication Enabled Successfully!" });
            }
            return Json(new { success = false, message = "Invalid Code. Please try again." });
        }
    }
}