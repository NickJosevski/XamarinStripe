using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

using Newtonsoft.Json;

using Xamarin.Payments.Stripe;

namespace Tests.XamarinStripe
{
    [TestFixture]
    public class PaymentTests
    {
        private StripePayment _payment;

        [SetUp]
        public void bla()
        {
            _payment = new StripePayment("opWnpofZHJ9tqEnGszFZTfZAmHKmz9Yz");
        }


        public static StripeCreditCardInfo GetCC()
        {
            StripeCreditCardInfo cc = new StripeCreditCardInfo();
            cc.CVC = "1234";
            cc.ExpirationMonth = 6;
            cc.ExpirationYear = 2012;
            cc.Number = "4242424242424242";
            return cc;
        }

        public static StripePlanInfo GetPlanInfo()
        {
            return new StripePlanInfo
            {
                Amount = 1999,
                ID = "myplan",
                Interval = StripePlanInterval.Month,
                Name = "My standard plan",
                TrialPeriod = 7
            };
        }

        public static StripeInvoiceItemInfo GetInvoiceItemInfo()
        {
            return new StripeInvoiceItemInfo { Amount = 1999, Description = "Invoice item: " + Guid.NewGuid().ToString() };
        }

        [Test]
        public void SimpleCharge()
        {
            StripeCreditCardInfo cc = GetCC();
            StripeCharge charge = _payment.Charge(5001, "usd", cc, "Test charge");
            Console.WriteLine(charge);
            string charge_id = charge.ID;
            StripeCharge charge_info = _payment.GetCharge(charge_id);
            Console.WriteLine(charge_info);

            StripeCharge refund = _payment.Refund(charge_info.ID);
            Console.WriteLine(refund.Created);
        }

        [Test]
        public void PartialRefund()
        {
            StripeCreditCardInfo cc = GetCC();
            StripeCharge charge = _payment.Charge(5001, "usd", cc, "Test partial refund");
            Console.WriteLine(charge.ID);
            StripeCharge refund = _payment.Refund(charge.ID, 2499);
            Console.WriteLine(refund.Amount);
        }

        [Test]
        public void Customer()
        {
            StripeCustomerInfo customer = new StripeCustomerInfo();
            //customer.Card = GetCC ();
            StripeCustomer customer_resp = _payment.CreateCustomer(customer);
            string customer_id = customer_resp.Id;
            StripeCustomer customer_info = _payment.GetCustomer(customer_id);
            Console.WriteLine(customer_info);
            StripeCustomer ci2 = _payment.DeleteCustomer(customer_id);
            
            //if (ci2.Deleted == false) throw new Exception("Failed to delete " + customer_id);
            Assert.IsTrue(ci2.Deleted, "Failed to delete " + customer_id);
        }

        [Test]
        public void CustomerAndCharge()
        {
            StripeCustomerInfo customer = new StripeCustomerInfo();
            //customer.Card = GetCC ();
            StripeCustomer response = _payment.CreateCustomer(customer);
            string customer_id = response.Id;
            StripeCustomer customer_info = _payment.GetCustomer(customer_id);
            Console.WriteLine(customer_info);
            StripeCustomerInfo info_update = new StripeCustomerInfo();
            info_update.Card = GetCC();
            StripeCustomer update_resp = _payment.UpdateCustomer(customer_id, info_update);
            Console.Write("Customer updated with CC. Press ENTER to continue...");
            Console.Out.Flush();
            Console.ReadLine();
            StripeCustomer ci2 = _payment.DeleteCustomer(customer_id);

            //if (ci2.Deleted == false) throw new Exception("Failed to delete " + customer_id);
            Assert.IsTrue(ci2.Deleted, "Failed to delete " + customer_id);
        }

        [Test]
        public void GetCharges()
        {
            List<StripeCharge> charges = _payment.GetCharges(0, 10);
            Console.WriteLine(charges.Count);
        }

        [Test]
        public void GetCustomers()
        {
            List<StripeCustomer> customers = _payment.GetCustomers(0, 10);
            Console.WriteLine(customers.Count);
        }

        [Test]
        public void CreateGetToken()
        {
            StripeCreditCardToken tok = _payment.CreateToken(GetCC());
            StripeCreditCardToken tok2 = _payment.GetToken(tok.ID);
        }

        [Test]
        public void CreatePlanGetPlan()
        {
            StripePlan plan = CreatePlan(_payment);
            int total;
            List<StripePlan> plans = _payment.GetPlans(10, 10, out total);
            Console.WriteLine(total);
        }

        public static StripePlan CreatePlan(StripePayment payment)
        {
            StripePlan plan = payment.CreatePlan(GetPlanInfo());
            StripePlan plan2 = payment.GetPlan(plan.ID);
            //DeletePlan (plan2, _payment);
            return plan2;
        }

        public static StripePlan DeletePlan(StripePlan plan, StripePayment payment)
        {
            var deleted = payment.DeletePlan(plan.ID);
            return deleted;
        }

        [Test]
        public void CreateSubscription()
        {
            StripeCustomer cust = _payment.CreateCustomer(new StripeCustomerInfo { Card = GetCC() });
            //StripePlan temp = new StripePlan { ID = "myplan" };
            //DeletePlan (temp, _payment);
            StripePlan plan = CreatePlan(_payment);
            StripeSubscription sub = _payment.Subscribe(
                cust.Id, new StripeSubscriptionInfo { Card = GetCC(), Plan = "myplan", Prorate = true });

            StripeSubscription sub2 = _payment.GetSubscription(sub.CustomerID);

            TestDeleteSubscription(cust, _payment);
            DeletePlan(plan, _payment);
        }

        public static StripeSubscription TestDeleteSubscription(StripeCustomer customer, StripePayment payment)
        {
            return payment.Unsubscribe(customer.Id, true);
        }

        [Test]
        public void CreateInvoiceItems()
        {
            StripeCustomer cust = _payment.CreateCustomer(new StripeCustomerInfo());
            StripeInvoiceItemInfo info = GetInvoiceItemInfo();
            info.CustomerId = cust.Id;
            StripeInvoiceItem item = _payment.CreateInvoiceItem(info);
            StripeInvoiceItemInfo newInfo = GetInvoiceItemInfo();
            newInfo.Description = "Invoice item: " + Guid.NewGuid().ToString();
            StripeInvoiceItem item2 = _payment.UpdateInvoiceItem(item.Id, newInfo);
            StripeInvoiceItem item3 = _payment.GetInvoiceItem(item2.Id);
            //if (item.Description == item3.Description) throw new Exception("Update failed");
            Assert.AreNotEqual(item.Description, item3.Description, "Update failed");

            StripeInvoiceItem deleted = _payment.DeleteInvoiceItem(item2.Id);

            //if (!deleted.Deleted.HasValue && deleted.Deleted.Value) throw new Exception("Delete failed");
            Assert.IsTrue(deleted.Deleted.Value, "Delete failed");

            int total;

            var items = _payment.GetInvoiceItems(10, 10, null, out total);
            Console.WriteLine(total);
            _payment.DeleteCustomer(cust.Id);
        }

        [Test]
        public void Invoices()
        {
            List<StripeInvoice> invoices = _payment.GetInvoices(10, 10);
            StripeInvoice inv = _payment.GetInvoice(invoices[0].ID);
            StripeCustomer cust = _payment.CreateCustomer(new StripeCustomerInfo());
            StripeSubscription sub = _payment.Subscribe(cust.Id, new StripeSubscriptionInfo { Card = GetCC() });
            StripeInvoice inv2 = _payment.GetUpcomingInvoice(cust.Id);
            _payment.Unsubscribe(cust.Id, true);
            _payment.DeleteCustomer(cust.Id);
        }

        [Test]
        public void Invoices2()
        {
            StripeCustomer cust = _payment.GetCustomer("cus_ulcOcy5Seu2dpq");
            StripePlanInfo planInfo = new StripePlanInfo
            {
                Amount = 1999,
                ID = "testplan",
                Interval = StripePlanInterval.Month,
                Name = "The Test Plan",
                //TrialPeriod = 7
            };
            //_payment.DeletePlan (planInfo.ID);
            StripePlan plan = _payment.CreatePlan(planInfo);
            StripeSubscriptionInfo subInfo = new StripeSubscriptionInfo { Card = GetCC(), Plan = planInfo.ID, Prorate = true };
            StripeSubscription sub = _payment.Subscribe(cust.Id, subInfo);
            _payment.CreateInvoiceItem(
                new StripeInvoiceItemInfo { CustomerId = cust.Id, Amount = 1337, Description = "Test single charge" });

            int total;
            List<StripeInvoice> invoices = _payment.GetInvoices(0, 10, cust.Id, out total);
            StripeInvoice upcoming = _payment.GetUpcomingInvoice(cust.Id);
            _payment.Unsubscribe(cust.Id, true);
            _payment.DeletePlan(planInfo.ID);
            foreach (StripeInvoiceLineItem line in upcoming)
            {
                Console.WriteLine("{0} for type {1}", line.Amount, line.GetType());
            }

        }

        [Test]
        public void DeserializePastDue()
        {
            string json = @"{
  ""status"": ""past_due"",
}";
            var sub = JsonConvert.DeserializeObject<StripeSubscription>(json);

            //if (sub.Status != StripeSubscriptionStatus.PastDue) throw new Exception("Failed to deserialize `StripeSubscriptionStatus.PastDue`");
            Assert.IsTrue(sub.Status == StripeSubscriptionStatus.PastDue, "Failed to deserialize `StripeSubscriptionStatus.PastDue`");
            string json2 = JsonConvert.SerializeObject(sub);
        }
    }
}
