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

        public static StripeCreditCardInfo GetValidCreditCard()
        {
            StripeCreditCardInfo cc = new StripeCreditCardInfo();
            cc.CVC = "123";
            cc.ExpirationMonth = DateTime.Now.Month + 2;
            cc.ExpirationYear = DateTime.Now.Year + 1;
            cc.Number = "4242424242424242";
            return cc;
        }

        public static StripePlanInfo GetPlanInfo()
        {
            return new StripePlanInfo
            {
                Amount = 1999,
                Id = "myplan" + DateTime.Now.Ticks,
                Interval = StripePlanInterval.month,
                Name = "My standard plan",
                TrialPeriod = 7
            };
        }

        public static StripeInvoiceItemInfo GetInvoiceItemInfo()
        {
            return new StripeInvoiceItemInfo { Amount = 1999, Description = "Invoice item: " + Guid.NewGuid().ToString() };
        }

        public static StripeInvoiceItemUpdateInfo GetInvoiceItemUpdateInfo()
        {
            return new StripeInvoiceItemUpdateInfo { Amount = 1999, Description = "Invoice item: " + Guid.NewGuid().ToString() };
        }

        [Test]
        public void SimpleCharge()
        {
            StripeCreditCardInfo cc = GetValidCreditCard();
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
            StripeCreditCardInfo cc = GetValidCreditCard();
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
            info_update.Card = GetValidCreditCard();
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
            StripeCreditCardToken tok = _payment.CreateToken(GetValidCreditCard());
            StripeCreditCardToken tok2 = _payment.GetToken(tok.ID);
        }

        [Test]
        public void CreatePlanGetPlan()
        {
            var plan = CreatePlan(_payment);
            int total;
            var plans = _payment.GetPlans(10, 10, out total);
            Console.WriteLine(total);
        }

        public static StripePlan CreatePlan(StripePayment payment)
        {
            StripePlan plan = payment.CreatePlan(GetPlanInfo());
            StripePlan plan2 = payment.GetPlan(plan.Id);
            //DeletePlan (plan2, _payment);
            return plan2;
        }

        public static StripePlan DeletePlan(StripePlan plan, StripePayment payment)
        {
            var deleted = payment.DeletePlan(plan.Id);
            return deleted;
        }

        [Test]
        public void AttemptToRetrieveNonExistantPlan()
        {
            // Arrange
            var randomId = "Should-Not-Exist" + Guid.NewGuid() + DateTime.Now.Ticks;

            // Act
            var ex = Assert.Throws<StripeException>(() => _payment.GetPlan(randomId));

            // Assert
            Assert.That(ex.StripeError.Message, Is.StringContaining("No such plan"));
            Assert.That(ex.StripeError.ErrorType, Is.StringContaining("invalid_request_error"));
        }

        [Test]
        public void CheckIfPlanExists_FalseForRandomPlan()
        {
            // Arrange
            var randomId = "Should-Not-Exist--" + Guid.NewGuid() + DateTime.Now.Ticks;

            // Act && Assert
            Assert.IsFalse(_payment.PlanExists(randomId));
        }

        [Test]
        public void CheckIfPlanExists_TrueForOneJustCreated()
        {
            // Arrange
            var id = "P-" + Guid.NewGuid();
            var planToCreate = GetPlanInfo();
            planToCreate.Id = id;
            var plan = _payment.CreatePlan(planToCreate);

            // Act && Assert
            Assert.IsTrue(_payment.PlanExists(id));
        }

        [Test]
        public void CreateSubscription()
        {
            StripeCustomer cust = _payment.CreateCustomer(new StripeCustomerInfo { Card = GetValidCreditCard() });
            //StripePlan temp = new StripePlan { ID = "myplan" };
            //DeletePlan (temp, _payment);
            StripePlan plan = CreatePlan(_payment);
            StripeSubscription sub = _payment.Subscribe(
                cust.Id, new StripeSubscriptionInfo { Card = GetValidCreditCard(), Plan = "myplan", Prorate = true });

            StripeSubscription sub2 = _payment.GetSubscription(sub.CustomerID);

            TestDeleteSubscription(cust, _payment);
            DeletePlan(plan, _payment);
        }

        public static StripeSubscription TestDeleteSubscription(StripeCustomer customer, StripePayment payment)
        {
            return payment.Unsubscribe(customer.Id, true);
        }

        [Test]
        public void StripeInvoiceItemUpdateInfo_ReportsWhenCustomerIdIsSupplied_ItShouldNotBe()
        {

            StripeCustomer cust = _payment.CreateCustomer(new StripeCustomerInfo());
            StripeInvoiceItemInfo info = GetInvoiceItemInfo();
            info.CustomerId = cust.Id;

            StripeInvoiceItem item = _payment.CreateInvoiceItem(info);

            StripeInvoiceItemUpdateInfo updateInfo = GetInvoiceItemUpdateInfo();
            updateInfo.Description = "Invoice item: " + Guid.NewGuid().ToString();
            updateInfo.CustomerId = cust.Id;
            
            var msg = Assert.Throws<ArgumentException>( () => _payment.UpdateInvoiceItem(item.Id, updateInfo)).Message;
            Assert.That(msg, Is.StringContaining("CustomerId should not be supplied"));
        }

        [Test]
        public void CreateInvoiceItems()
        {
            StripeCustomer cust = _payment.CreateCustomer(new StripeCustomerInfo());
            StripeInvoiceItemInfo info = GetInvoiceItemInfo();
            info.CustomerId = cust.Id;

            StripeInvoiceItem item = _payment.CreateInvoiceItem(info);

            StripeInvoiceItemUpdateInfo updateInfo = GetInvoiceItemUpdateInfo();
            updateInfo.Description = "Invoice item: " + Guid.NewGuid().ToString();
            StripeInvoiceItem item2 = _payment.UpdateInvoiceItem(item.Id, updateInfo);

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
            List<StripeInvoice> invoices = _payment.GetInvoices(1, 2);
            var plans = _payment.GetPlans(0, 1);

            Assert.IsTrue(invoices.Count > 0);
            Assert.IsTrue(plans.Count > 0);

            StripeInvoice inv = _payment.GetInvoice(invoices[0].ID);
            StripeCustomer cust = _payment.CreateCustomer(new StripeCustomerInfo());
            StripeSubscription sub = _payment.Subscribe(cust.Id, new StripeSubscriptionInfo
                {
                    Plan = plans[0].Id,
                    Card = GetValidCreditCard()
                });
            StripeInvoice inv2 = _payment.GetUpcomingInvoice(cust.Id);
            _payment.Unsubscribe(cust.Id, true);
            _payment.DeleteCustomer(cust.Id);
        }

        [Test]
        public void Invoices2()
        {
            var customerJustCreated = _payment.CreateCustomer(new StripeCustomerInfo());

            var cust = _payment.GetCustomer(customerJustCreated.Id);
            var planInfo = new StripePlanInfo
            {
                Amount = 1999,
                Id = "testplan" + DateTime.Now.Ticks,
                Interval = StripePlanInterval.month,
                Name = "The Test Plan",
                //TrialPeriod = 7
            };

            StripePlan plan = _payment.CreatePlan(planInfo);
            StripeSubscriptionInfo subInfo = new StripeSubscriptionInfo { Card = GetValidCreditCard(), Plan = planInfo.Id, Prorate = true };
            StripeSubscription sub = _payment.Subscribe(cust.Id, subInfo);
            _payment.CreateInvoiceItem(
                new StripeInvoiceItemInfo { CustomerId = cust.Id, Amount = 1337, Description = "Test single charge" });

            int total;
            List<StripeInvoice> invoices = _payment.GetInvoices(0, 10, cust.Id, out total);
            Assert.IsNotEmpty(invoices);

            StripeInvoice upcoming = _payment.GetUpcomingInvoice(cust.Id);
            _payment.Unsubscribe(cust.Id, true);
            _payment.DeletePlan(planInfo.Id);
            foreach (StripeInvoiceLineItem line in upcoming)
            {
                Console.WriteLine("{0} for type {1}", line.Amount, line.GetType());
            }

            _payment.DeletePlan(planInfo.Id);
        }

        [Test]
        public void DeserializePastDue()
        {
            string json = @"{
  ""status"": ""past_due"",
}";
            var sub = JsonConvert.DeserializeObject<StripeSubscription>(json);

            //if (sub.Status != StripeSubscriptionStatus.PastDue) throw new Exception("Failed to deserialize `StripeSubscriptionStatus.PastDue`");
            Assert.IsTrue(sub.Status == StripeSubscriptionStatus.past_due, "Failed to deserialize `StripeSubscriptionStatus.PastDue`");
            string json2 = JsonConvert.SerializeObject(sub);
        }

        [Test]
        public void DeserializeStripeChargeCollection()
        {
            var data =
                @" 
{
  ""count"": 7,
  ""data"": [
    {
      ""amount_refunded"": 5001,
      ""description"": ""Test charge"",
      ""created"": 1341283214,
      ""fee_details"": [
        {
          ""type"": ""stripe_fee"",
          ""description"": ""Stripe processing fees"",
          ""currency"": ""usd"",
          ""amount"": 175,
          ""application"": null
        }
      ],
      ""object"": ""charge"",
      ""fee"": 175,
      ""currency"": ""usd"",
      ""disputed"": false,
      ""failure_message"": null,
      ""paid"": true,
      ""livemode"": false,
      ""invoice"": null,
      ""amount"": 5001,
      ""customer"": null,
      ""refunded"": true,
      ""id"": ""ch_y3hRCSOrlDEQOO"",
      ""card"": {
        ""type"": ""Visa"",
        ""address_line1_check"": null,
        ""address_state"": null,
        ""exp_month"": 9,
        ""address_country"": null,
        ""exp_year"": 2013,
        ""object"": ""card"",
        ""address_line1"": null,
        ""cvc_check"": ""pass"",
        ""address_line2"": null,
        ""name"": null,
        ""fingerprint"": ""MjkuavAYKoQdUg20"",
        ""address_zip_check"": null,
        ""address_zip"": null,
        ""country"": ""US"",
        ""last4"": ""4242""
      }
    }
  ]
}
";
            var result = JsonConvert.DeserializeObject<StripeChargeCollection>(data);
            Assert.IsNotEmpty(result.ToList());
            Assert.That(result.ToList()[0].FeeDetails.Count, Is.EqualTo(1));
            Assert.That(result.ToList()[0].Card.Type, Is.EqualTo("Visa"));
        }

    }
}
