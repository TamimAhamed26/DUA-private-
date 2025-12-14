using MDUA.DataAccess;
using MDUA.DataAccess.Interface;
using MDUA.Entities;
using MDUA.Entities.Bases;
using MDUA.Entities.List;
using MDUA.Facade.Interface;
using MDUA.Framework.DataAccess;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace MDUA.Facade
{
    public class PaymentFacade : IPaymentFacade
    {
        private readonly ICustomerPaymentDataAccess _customerPaymentDataAccess;
        private readonly IInventoryTransactionDataAccess _inventoryTransactionDataAccess;
        private readonly ISalesOrderDetailDataAccess _salesOrderDetailDataAccess;
        private readonly ICompanyPaymentMethodDataAccess _companyPaymentMethodDataAccess;
        private readonly IConfiguration _configuration;
         private readonly ISalesOrderHeaderDataAccess _orderDA;
         private readonly ICustomerPaymentDataAccess _paymentDA;


        public PaymentFacade(
            ICustomerPaymentDataAccess customerPaymentDA,
            IInventoryTransactionDataAccess inventoryTransactionDA,
            ISalesOrderDetailDataAccess salesOrderDetailDA,
            ICompanyPaymentMethodDataAccess companyPaymentMethodDA, IConfiguration configuration, ISalesOrderHeaderDataAccess orderDA, ICustomerPaymentDataAccess paymentDA)
        {
            _customerPaymentDataAccess = customerPaymentDA;
            _inventoryTransactionDataAccess = inventoryTransactionDA;
            _salesOrderDetailDataAccess = salesOrderDetailDA;
            _companyPaymentMethodDataAccess = companyPaymentMethodDA;
            _configuration = configuration;
            _orderDA = orderDA;
            _paymentDA = paymentDA;
        }

        // 1. Helper to populate Dropdown
        public List<CompanyPaymentMethod> GetActivePaymentMethods(int companyId)
        {
            // Implement query to join CompanyPaymentMethod + PaymentMethod tables
            return _companyPaymentMethodDataAccess.GetActiveByCompany(companyId);
        }

        // 2. The Main Logic
        public long AddPayment(CustomerPayment payment)

        {

            // A. Get the Connection String from Configuration

            string connString = _configuration.GetConnectionString("DefaultConnection");

            // B. Pass the string to static BaseDataAccess method

            SqlTransaction transaction = BaseDataAccess.BeginTransaction(connString);

            try

            {

                // C. Instantiate DAs with the transaction

                var salesDa = new SalesOrderDetailDataAccess(transaction);

                var invDa = new InventoryTransactionDataAccess(transaction);

                var custPayDa = new CustomerPaymentDataAccess(transaction);

                // --- Business Logic ---

                var orderDetail = salesDa.GetFirstDetailByOrderRef(payment.TransactionReference);

                int? invTrxId = null;

                if (orderDetail != null && orderDetail.ProductVariantId > 0)

                {

                    var invTrx = new InventoryTransaction

                    {

                        SalesOrderDetailId = orderDetail.Id,

                        InOut = "IN",

                        Date = DateTime.Now,

                        Price = payment.Amount,

                        Quantity = orderDetail.Quantity,

                        ProductVariantId = orderDetail.ProductVariantId,

                        Remarks = "Payment: " + payment.Notes,

                        CreatedBy = payment.CreatedBy,

                        CreatedAt = DateTime.Now

                    };

                    long newId = invDa.Insert(invTrx);

                    if (newId > 0) invTrxId = (int)newId;

                }

                payment.InventoryTransactionId = invTrxId;

                // Insert Payment

                long paymentId = custPayDa.Insert(payment);

                // --- End Business Logic ---

                // D. Commit

                BaseDataAccess.CloseTransaction(true, transaction);

                return paymentId;

            }

            catch (Exception ex)

            {

                // E. Rollback

                BaseDataAccess.CloseTransaction(false, transaction);

                throw;

            }

        }


        public long Insert(CustomerPaymentBase entity)
        {
            return _customerPaymentDataAccess.Insert(entity);
        }

        public long Update(CustomerPaymentBase entity)
        {
            return _customerPaymentDataAccess.Update(entity);
        }

        public long Delete(int id)
        {
            return _customerPaymentDataAccess.Delete(id);
        }

        public CustomerPayment Get(int id)
        {
            return _customerPaymentDataAccess.Get(id);
        }

        public CustomerPaymentList GetAll()
        {
            return _customerPaymentDataAccess.GetAll();
        }

        public CustomerPaymentList GetByQuery(string query)
        {
            return _customerPaymentDataAccess.GetByQuery(query);
        }


    }
}