﻿/*
 * Copyright 2011 Xamarin, Inc., Joe Dluzen
 *
 * Author(s):
 *  Gonzalo Paniagua Javier (gonzalo@xamarin.com)
 *  Joe Dluzen (jdluzen@gmail.com)
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using Xamarin.Payments.Stripe;
using Newtonsoft.Json;

namespace PaymentTest
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var payment = new StripePayment("opWnpofZHJ9tqEnGszFZTfZAmHKmz9Yz");
            TestSimpleCharge(payment);
            TestPartialRefund(payment);
            TestCustomer(payment);
            TestCustomerAndCharge(payment);
            TestGetCharges(payment);
            TestGetCustomers(payment);
            TestCreateGetToken(payment);
            TestCreatePlanGetPlan(payment);
            TestCreateSubscription(payment);
            TestCreateInvoiceItems(payment);
            TestInvoices(payment);
            TestInvoices2(payment);
            TestDeserializePastDue();

            Console.ReadKey();
        }

        private static StripeCreditCardInfo GetCC()
        {
            StripeCreditCardInfo cc = new StripeCreditCardInfo();
            cc.CVC = "1234";
            cc.ExpirationMonth = 6;
            cc.ExpirationYear = 2012;
            cc.Number = "4242424242424242";
            return cc;
        }

        private static StripePlanInfo GetPlanInfo()
        {
            return new StripePlanInfo
                {
                    Amount = 1999,
                    Id = "myplan",
                    Interval = StripePlanInterval.month,
                    Name = "My standard plan",
                    TrialPeriod = 7
                };
        }

        private static StripeInvoiceItemInfo GetInvoiceItemInfo()
        {
            return new StripeInvoiceItemInfo
                { Amount = 1999, Description = "Invoice item: " + Guid.NewGuid().ToString() };
        }

        private static StripeInvoiceItemUpdateInfo GetInvoiceItemUpdateInfo()
        {
            return new StripeInvoiceItemUpdateInfo { Amount = 1999, Description = "Invoice item: " + Guid.NewGuid().ToString() };
        }

        private static void TestSimpleCharge(StripePayment payment)
        {
            StripeCreditCardInfo cc = GetCC();
            StripeCharge charge = payment.Charge(5001, "usd", cc, "Test charge");
            Console.WriteLine(charge);
            string charge_id = charge.Id;
            StripeCharge charge_info = payment.GetCharge(charge_id);
            Console.WriteLine(charge_info);

            StripeCharge refund = payment.Refund(charge_info.Id);
            Console.WriteLine(refund.Created);
        }

        private static void TestPartialRefund(StripePayment payment)
        {
            StripeCreditCardInfo cc = GetCC();
            StripeCharge charge = payment.Charge(5001, "usd", cc, "Test partial refund");
            Console.WriteLine(charge.Id);
            StripeCharge refund = payment.Refund(charge.Id, 2499);
            Console.WriteLine(refund.Amount);
        }

        private static void TestCustomer(StripePayment payment)
        {
            StripeCustomerInfo customer = new StripeCustomerInfo();
            //customer.Card = GetCC ();
            StripeCustomer customer_resp = payment.CreateCustomer(customer);
            string customer_id = customer_resp.Id;
            StripeCustomer customer_info = payment.GetCustomer(customer_id);
            Console.WriteLine(customer_info);
            StripeCustomer ci2 = payment.DeleteCustomer(customer_id);
            if (ci2.Deleted == false) throw new Exception("Failed to delete " + customer_id);
        }

        private static void TestCustomerAndCharge(StripePayment payment)
        {
            StripeCustomerInfo customer = new StripeCustomerInfo();
            //customer.Card = GetCC ();
            StripeCustomer response = payment.CreateCustomer(customer);
            string customer_id = response.Id;
            StripeCustomer customer_info = payment.GetCustomer(customer_id);
            Console.WriteLine(customer_info);
            StripeCustomerInfo info_update = new StripeCustomerInfo();
            info_update.Card = GetCC();
            StripeCustomer update_resp = payment.UpdateCustomer(customer_id, info_update);
            Console.Write("Customer updated with CC. Press ENTER to continue...");
            Console.Out.Flush();
            Console.ReadLine();
            StripeCustomer ci2 = payment.DeleteCustomer(customer_id);
            if (ci2.Deleted == false) throw new Exception("Failed to delete " + customer_id);
        }

        private static void TestGetCharges(StripePayment payment)
        {
            List<StripeCharge> charges = payment.GetCharges(0, 10);
            Console.WriteLine(charges.Count);
        }

        private static void TestGetCustomers(StripePayment payment)
        {
            List<StripeCustomer> customers = payment.GetCustomers(0, 10);
            Console.WriteLine(customers.Count);
        }

        private static void TestCreateGetToken(StripePayment payment)
        {
            StripeCreditCardToken tok = payment.CreateToken(GetCC());
            StripeCreditCardToken tok2 = payment.GetToken(tok.Id);
        }

        private static void TestCreatePlanGetPlan(StripePayment payment)
        {
            StripePlan plan = CreatePlan(payment);
            int total;
            List<StripePlan> plans = payment.GetPlans(10, 10, out total);
            Console.WriteLine(total);
        }

        private static StripePlan CreatePlan(StripePayment payment)
        {
            StripePlan plan = payment.CreatePlan(GetPlanInfo());
            StripePlan plan2 = payment.GetPlan(plan.Id);
            //DeletePlan (plan2, payment);
            return plan2;
        }

        private static StripePlan DeletePlan(StripePlan plan, StripePayment payment)
        {
            StripePlan deleted = payment.DeletePlan(plan.Id);
            return deleted;
        }

        private static void TestCreateSubscription(StripePayment payment)
        {
            StripeCustomer cust = payment.CreateCustomer(new StripeCustomerInfo { Card = GetCC() });
            //StripePlan temp = new StripePlan { Id = "myplan" };
            //DeletePlan (temp, payment);
            StripePlan plan = CreatePlan(payment);
            StripeSubscription sub = payment.Subscribe(
                cust.Id, new StripeSubscriptionInfo { Card = GetCC(), Plan = "myplan", Prorate = true });

            StripeSubscription sub2 = payment.GetSubscription(sub.CustomerID);

            TestDeleteSubscription(cust, payment);
            DeletePlan(plan, payment);
        }

        private static StripeSubscription TestDeleteSubscription(StripeCustomer customer, StripePayment payment)
        {
            return payment.Unsubscribe(customer.Id, true);
        }

        private static void TestCreateInvoiceItems(StripePayment payment)
        {
            StripeCustomer cust = payment.CreateCustomer(new StripeCustomerInfo());
            StripeInvoiceItemInfo info = GetInvoiceItemInfo();
            info.CustomerId = cust.Id;
            StripeInvoiceItem item = payment.CreateInvoiceItem(info);
            StripeInvoiceItemUpdateInfo updateInfo = GetInvoiceItemUpdateInfo();
            updateInfo.Description = "Invoice item: " + Guid.NewGuid().ToString();
            StripeInvoiceItem item2 = payment.UpdateInvoiceItem(item.Id, updateInfo);
            StripeInvoiceItem item3 = payment.GetInvoiceItem(item2.Id);
            if (item.Description == item3.Description) throw new Exception("Update failed");
            StripeInvoiceItem deleted = payment.DeleteInvoiceItem(item2.Id);
            if (!deleted.Deleted.HasValue && deleted.Deleted.Value) throw new Exception("Delete failed");
            int total;

            var items = payment.GetInvoiceItems(10, 10, null, out total);
            Console.WriteLine(total);
            payment.DeleteCustomer(cust.Id);
        }

        private static void TestInvoices(StripePayment payment)
        {
            List<StripeInvoice> invoices = payment.GetInvoices(10, 10);
            StripeInvoice inv = payment.GetInvoice(invoices[0].Id);
            StripeCustomer cust = payment.CreateCustomer(new StripeCustomerInfo());
            StripeSubscription sub = payment.Subscribe(cust.Id, new StripeSubscriptionInfo { Card = GetCC() });
            StripeInvoice inv2 = payment.GetUpcomingInvoice(cust.Id);
            payment.Unsubscribe(cust.Id, true);
            payment.DeleteCustomer(cust.Id);
        }

        private static void TestInvoices2(StripePayment payment)
        {
            StripeCustomer cust = payment.GetCustomer("cus_ulcOcy5Seu2dpq");
            StripePlanInfo planInfo = new StripePlanInfo
                {
                    Amount = 1999,
                    Id = "testplan",
                    Interval = StripePlanInterval.month,
                    Name = "The Test Plan",
                    //TrialPeriod = 7
                };
            //payment.DeletePlan (planInfo.Id);
            StripePlan plan = payment.CreatePlan(planInfo);
            StripeSubscriptionInfo subInfo = new StripeSubscriptionInfo
                { Card = GetCC(), Plan = planInfo.Id, Prorate = true };
            StripeSubscription sub = payment.Subscribe(cust.Id, subInfo);
            payment.CreateInvoiceItem(
                new StripeInvoiceItemInfo { CustomerId = cust.Id, Amount = 1337, Description = "Test single charge" });

            int total;
            List<StripeInvoice> invoices = payment.GetInvoices(0, 10, cust.Id, out total);
            StripeInvoice upcoming = payment.GetUpcomingInvoice(cust.Id);
            payment.Unsubscribe(cust.Id, true);
            payment.DeletePlan(planInfo.Id);
            foreach (StripeInvoiceLineItem line in upcoming)
            {
                Console.WriteLine("{0} for type {1}", line.Amount, line.GetType());
            }

        }

        private static void TestDeserializePastDue()
        {
            string json = @"{
  ""status"": ""past_due"",
}";
            StripeSubscription sub = JsonConvert.DeserializeObject<StripeSubscription>(json);
            if (sub.Status != StripeSubscriptionStatus.past_due) throw new Exception("Failed to deserialize `StripeSubscriptionStatus.PastDue`");
            string json2 = JsonConvert.SerializeObject(sub);
        }
    }
}
