﻿/*
 * Copyright 2011 Xamarin, Inc., 2011 - 2012 Joe Dluzen
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
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web;

using Newtonsoft.Json;

namespace Xamarin.Payments.Stripe
{
    public class StripePayment
    {
        private static readonly string api_endpoint = "https://api.stripe.com/v1";

        private static readonly string subscription_path = "{0}/customers/{1}/subscription";

        private static readonly string user_agent = "Stripe .NET v1";

        private static readonly Encoding encoding = Encoding.UTF8;

        private readonly ICredentials _credential;

        public StripePayment(string api_key)
        {
            _credential = new NetworkCredential(api_key, "");
            TimeoutSeconds = 30;
        }

        #region Shared

        protected virtual WebRequest SetupRequest(string method, string url)
        {
            var req = WebRequest.Create(url);
            req.Method = method;
            if (req is HttpWebRequest)
            {
                ((HttpWebRequest)req).UserAgent = user_agent;
            }
            req.Credentials = _credential;
            req.PreAuthenticate = true;
            req.Timeout = TimeoutSeconds * 1000;
            if (method == "POST") req.ContentType = "application/x-www-form-urlencoded";
            return req;
        }

        private static string GetResponseAsString(WebResponse response)
        {
            using (var sr = new StreamReader(response.GetResponseStream(), encoding))
            {
                return sr.ReadToEnd();
            }
        }

        protected virtual string DoRequest(string endpoint)
        {
            return DoRequest(endpoint, "GET", null);
        }

        protected virtual string DoRequest(string endpoint, string method, string body)
        {
            string result = null;
            WebRequest req = SetupRequest(method, endpoint);
            if (body != null)
            {
                byte[] bytes = encoding.GetBytes(body.ToString());
                req.ContentLength = bytes.Length;
                using (Stream st = req.GetRequestStream())
                {
                    st.Write(bytes, 0, bytes.Length);
                }
            }

            try
            {
                using (var resp = req.GetResponse())
                {
                    result = GetResponseAsString(resp);
                }
            }
            catch (WebException wexc)
            {
                if (wexc.Response != null)
                {
                    var json_error = GetResponseAsString(wexc.Response);
                    var status_code = HttpStatusCode.BadRequest;
                    var resp = wexc.Response as HttpWebResponse;
                    if (resp != null) status_code = resp.StatusCode;

                    if ((int)status_code <= 500) throw StripeException.GetFromJSON(status_code, json_error);
                }
                throw;
            }
            return result;
        }

        protected virtual StringBuilder UrlEncode(IUrlEncoderInfo infoInstance)
        {
            var str = new StringBuilder();
            infoInstance.UrlEncode(str);
            if (str.Length > 0) str.Length--;
            return str;
        }

        #endregion

        #region Charge

        public StripeCharge Charge(int amount_cents, string currency, string customer, string description)
        {
            if (string.IsNullOrEmpty(customer)) throw new ArgumentNullException("customer");

            return Charge(amount_cents, currency, customer, null, description);
        }

        public StripeCharge Charge(int amount_cents, string currency, StripeCreditCardInfo card, string description)
        {
            if (card == null) throw new ArgumentNullException("card");

            return Charge(amount_cents, currency, null, card, description);
        }

        private StripeCharge Charge(
            int amount_cents, string currency, string customer, StripeCreditCardInfo card, string description)
        {
            if (amount_cents < 0) throw new ArgumentOutOfRangeException("amount_cents", "Must be greater than or equal 0");
            if (string.IsNullOrEmpty(currency)) throw new ArgumentNullException("currency");
            currency = currency.ToLower();
            if (currency != "usd") throw new ArgumentException("The only supported currency is 'usd'");

            var str = new StringBuilder();
            str.AppendFormat("amount={0}&", amount_cents);
            str.AppendFormat("currency={0}&", currency);
            if (!string.IsNullOrEmpty(description))
            {
                str.AppendFormat("description={0}&", HttpUtility.UrlEncode(description));
            }

            if (card != null)
            {
                card.UrlEncode(str);
            }
            else
            {
                // customer is non-empty
                str.AppendFormat("customer={0}&", HttpUtility.UrlEncode(customer));
            }
            str.Length--;
            string ep = String.Format("{0}/charges", api_endpoint);
            string json = DoRequest(ep, "POST", str.ToString());
            return JsonConvert.DeserializeObject<StripeCharge>(json);
        }

        public StripeCharge GetCharge(string charge_id)
        {
            if (string.IsNullOrEmpty(charge_id)) throw new ArgumentNullException("charge_id");

            var ep = String.Format("{0}/charges/{1}", api_endpoint, HttpUtility.UrlEncode(charge_id));
            var json = DoRequest(ep);
            return JsonConvert.DeserializeObject<StripeCharge>(json);
        }

        public List<StripeCharge> GetCharges()
        {
            return GetCharges(0, 10);
        }

        public List<StripeCharge> GetCharges(int offset, int count)
        {
            int dummy;
            return GetCharges(offset, count, null, out dummy);
        }

        public List<StripeCharge> GetCharges(int offset, int count, string customer_id)
        {
            int dummy;
            return GetCharges(offset, count, customer_id, out dummy);
        }

        public List<StripeCharge> GetCharges(int offset, int count, string customer_id, out int total)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset");
            if (count < 1 || count > 100) throw new ArgumentOutOfRangeException("count");

            var str = new StringBuilder();
            str.AppendFormat("offset={0}&", offset);
            str.AppendFormat("count={0}&", count);
            if (!string.IsNullOrEmpty(customer_id)) str.AppendFormat("customer={0}&", HttpUtility.UrlEncode(customer_id));

            str.Length--;
            var ep = String.Format("{0}/charges?{1}", api_endpoint, str);
            var json = DoRequest(ep);
            var charges = JsonConvert.DeserializeObject<StripeChargeCollection>(json);
            total = charges.Total;
            return charges.Charges;
        }

        #endregion

        #region Refund

        public StripeCharge Refund(string charge_id)
        {
            if (string.IsNullOrEmpty(charge_id)) throw new ArgumentNullException("charge_id");

            string ep = String.Format("{0}/charges/{1}/refund", api_endpoint, HttpUtility.UrlEncode(charge_id));
            string json = DoRequest(ep, "POST", null);
            return JsonConvert.DeserializeObject<StripeCharge>(json);
        }

        public StripeCharge Refund(string charge_id, int amount)
        {
            if (string.IsNullOrEmpty(charge_id)) throw new ArgumentNullException("charge_id");
            if (amount <= 0) throw new ArgumentException("Amount must be greater than zero.", "amount");

            string ep = String.Format(
                "{0}/charges/{1}/refund?amount={2}", api_endpoint, HttpUtility.UrlEncode(charge_id), amount);
            string json = DoRequest(ep, "POST", null);
            return JsonConvert.DeserializeObject<StripeCharge>(json);
        }

        #endregion

        #region Customer

        private StripeCustomer CreateOrUpdateCustomer(string id, StripeCustomerInfo customer)
        {
            var str = UrlEncode(customer);

            var format = "{0}/customers"; // Create
            if (id != null) format = "{0}/customers/{1}"; // Update
            var ep = String.Format(format, api_endpoint, HttpUtility.UrlEncode(id));
            var json = DoRequest(ep, "POST", str.ToString());
            return JsonConvert.DeserializeObject<StripeCustomer>(json);
        }

        public StripeCustomer CreateCustomer(StripeCustomerInfo customer)
        {
            if (customer == null) throw new ArgumentNullException("customer");

            return CreateOrUpdateCustomer(null, customer);
        }

        public StripeCustomer UpdateCustomer(string id, StripeCustomerInfo customer)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException("id");
            if (customer == null) throw new ArgumentNullException("customer");

            return CreateOrUpdateCustomer(id, customer);
        }

        public StripeCustomer GetCustomer(string customer_id)
        {
            if (string.IsNullOrEmpty(customer_id)) throw new ArgumentNullException("customer_id");

            var ep = String.Format("{0}/customers/{1}", api_endpoint, HttpUtility.UrlEncode(customer_id));
            var json = DoRequest(ep);
            return JsonConvert.DeserializeObject<StripeCustomer>(json);
        }

        public List<StripeCustomer> GetCustomers()
        {
            return GetCustomers(0, 10);
        }

        public List<StripeCustomer> GetCustomers(int offset, int count)
        {
            int dummy;
            return GetCustomers(offset, count, out dummy);
        }

        public List<StripeCustomer> GetCustomers(int offset, int count, out int total)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset");
            if (count < 1 || count > 100) throw new ArgumentOutOfRangeException("count");

            var str = String.Format("offset={0}&count={1}", offset, count);
            var ep = String.Format("{0}/customers?{1}", api_endpoint, str);
            var json = DoRequest(ep);
            var customers = JsonConvert.DeserializeObject<StripeCustomerCollection>(json);
            total = customers.Total;
            return customers.Customers;
        }

        public StripeCustomer DeleteCustomer(string customer_id)
        {
            if (string.IsNullOrEmpty(customer_id)) throw new ArgumentNullException("customer_id");

            string ep = String.Format("{0}/customers/{1}", api_endpoint, HttpUtility.UrlEncode(customer_id));
            string json = DoRequest(ep, "DELETE", null);
            return JsonConvert.DeserializeObject<StripeCustomer>(json);
        }

        #endregion

        #region Tokens

        public StripeCreditCardToken CreateToken(StripeCreditCardInfo card)
        {
            if (card == null) throw new ArgumentNullException("card");
            StringBuilder str = UrlEncode(card);

            string ep = string.Format("{0}/tokens", api_endpoint);
            string json = DoRequest(ep, "POST", str.ToString());
            return JsonConvert.DeserializeObject<StripeCreditCardToken>(json);
        }

        public StripeCreditCardToken GetToken(string tokenId)
        {
            if (string.IsNullOrEmpty(tokenId)) throw new ArgumentNullException(tokenId);

            string ep = string.Format("{0}/tokens/{1}", api_endpoint, HttpUtility.UrlEncode(tokenId));
            string json = DoRequest(ep);
            return JsonConvert.DeserializeObject<StripeCreditCardToken>(json);
        }

        #endregion

        #region Plans

        public StripePlan CreatePlan(StripePlanInfo plan)
        {
            if (plan == null) throw new ArgumentNullException("plan");
            var str = UrlEncode(plan);

            var ep = string.Format("{0}/plans", api_endpoint);
            var json = DoRequest(ep, "POST", str.ToString());
            return JsonConvert.DeserializeObject<StripePlan>(json);
        }

        public StripePlan GetPlan(string planId)
        {
            if (string.IsNullOrEmpty(planId)) throw new ArgumentNullException("id");

            var ep = string.Format("{0}/plans/{1}", api_endpoint, HttpUtility.UrlEncode(planId));
            var json = DoRequest(ep);
            return JsonConvert.DeserializeObject<StripePlan>(json);
        }

        public StripePlan DeletePlan(string planId)
        {
            if (string.IsNullOrEmpty(planId)) throw new ArgumentNullException("id");

            string ep = string.Format("{0}/plans/{1}", api_endpoint, HttpUtility.UrlEncode(planId));
            string json = DoRequest(ep, "DELETE", null);
            return JsonConvert.DeserializeObject<StripePlan>(json);
        }

        public List<StripePlan> GetPlans()
        {
            return GetPlans(0, 10);
        }

        public List<StripePlan> GetPlans(int offset, int count)
        {
            int dummy;
            return GetPlans(offset, count, out dummy);
        }

        public List<StripePlan> GetPlans(int offset, int count, out int total)
        {
            var str = string.Format("count={0}&offset={1}", count, offset);
            var ep = string.Format("{0}/plans?{1}", api_endpoint, str);
            var json = DoRequest(ep);
            var plans = JsonConvert.DeserializeObject<StripePlanCollection>(json);
            total = plans.Total;
            return plans.Plans;
        }

        #endregion

        #region Subscriptions

        public StripeSubscription Subscribe(string customerId, StripeSubscriptionInfo subscription)
        {
            var str = UrlEncode(subscription);
            var ep = string.Format(subscription_path, api_endpoint, HttpUtility.UrlEncode(customerId));
            var json = DoRequest(ep, "POST", str.ToString());
            return JsonConvert.DeserializeObject<StripeSubscription>(json);
        }

        public StripeSubscription GetSubscription(string customerId)
        {
            if (string.IsNullOrEmpty(customerId)) throw new ArgumentNullException("customerId");
            var ep = string.Format(subscription_path, api_endpoint, HttpUtility.UrlEncode(customerId));
            var json = DoRequest(ep);
            return JsonConvert.DeserializeObject<StripeSubscription>(json);
        }

        public StripeSubscription Unsubscribe(string customerId, bool atPeriodEnd)
        {
            string ep = string.Format(
                subscription_path + "?at_period_end={2}",
                api_endpoint,
                HttpUtility.UrlEncode(customerId),
                atPeriodEnd.ToString(CultureInfo.InvariantCulture).ToLowerInvariant());
            string json = DoRequest(ep, "DELETE", null);
            return JsonConvert.DeserializeObject<StripeSubscription>(json);
        }

        #endregion

        #region Invoice items

        public StripeInvoiceItem CreateInvoiceItem(StripeInvoiceItemInfo item)
        {
            if (string.IsNullOrEmpty(item.CustomerID)) throw new ArgumentNullException("item.CustomerID");
            StringBuilder str = UrlEncode(item);
            string ep = string.Format("{0}/invoiceitems", api_endpoint);
            string json = DoRequest(ep, "POST", str.ToString());
            return JsonConvert.DeserializeObject<StripeInvoiceItem>(json);
        }

        public StripeInvoiceItem GetInvoiceItem(string invoiceItemId)
        {
            if (string.IsNullOrEmpty(invoiceItemId)) throw new ArgumentNullException("invoiceItemId");
            string ep = string.Format("{0}/invoiceitems/{1}", api_endpoint, invoiceItemId);
            string json = DoRequest(ep);
            return JsonConvert.DeserializeObject<StripeInvoiceItem>(json);
        }

        public StripeInvoiceItem UpdateInvoiceItem(string invoiceItemId, StripeInvoiceItemInfo item)
        {
            var str = UrlEncode(item);
            string ep = string.Format("{0}/invoiceitems/{1}", api_endpoint, invoiceItemId);
            string json = DoRequest(ep, "POST", str.ToString());
            return JsonConvert.DeserializeObject<StripeInvoiceItem>(json);
        }

        public StripeInvoiceItem DeleteInvoiceItem(string invoiceItemId)
        {
            var ep = string.Format("{0}/invoiceitems/{1}", api_endpoint, invoiceItemId);
            var json = DoRequest(ep, "DELETE", null);
            return JsonConvert.DeserializeObject<StripeInvoiceItem>(json);
        }

        public List<StripeInvoiceItem> GetInvoiceItems()
        {
            return GetInvoiceItems(0, 10);
        }

        public List<StripeInvoiceItem> GetInvoiceItems(int offset, int count)
        {
            return GetInvoiceItems(offset, count, null);
        }

        public List<StripeInvoiceItem> GetInvoiceItems(int offset, int count, string customerId)
        {
            int dummy;
            return GetInvoiceItems(offset, count, customerId, out dummy);
        }

        public List<StripeInvoiceItem> GetInvoiceItems(int offset, int count, string customerId, out int total)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset");
            if (count < 1 || count > 100) throw new ArgumentOutOfRangeException("count");

            var str = new StringBuilder();
            str.AppendFormat("offset={0}&", offset);
            str.AppendFormat("count={0}&", count);
            if (!string.IsNullOrEmpty(customerId)) str.AppendFormat("customer={0}&", HttpUtility.UrlEncode(customerId));

            str.Length--;
            string ep = String.Format("{0}/invoiceitems?{1}", api_endpoint, str);
            string json = DoRequest(ep);
            var invoiceItems = JsonConvert.DeserializeObject<StripeInvoiceItemCollection>(json);
            total = invoiceItems.Total;
            return invoiceItems.InvoiceItems;
        }

        #endregion

        #region Invoices

        public StripeInvoice GetInvoice(string invoiceId)
        {
            if (string.IsNullOrEmpty(invoiceId)) throw new ArgumentNullException("invoiceId");
            string ep = string.Format("{0}/invoices/{1}", api_endpoint, invoiceId);
            string json = DoRequest(ep);
            return JsonConvert.DeserializeObject<StripeInvoice>(json);
        }

        public List<StripeInvoice> GetInvoices()
        {
            return GetInvoices(0, 10);
        }

        public List<StripeInvoice> GetInvoices(int offset, int count)
        {
            return GetInvoices(offset, count, null);
        }

        public List<StripeInvoice> GetInvoices(int offset, int count, string customerId)
        {
            int dummy;
            return GetInvoices(offset, count, customerId, out dummy);
        }

        public List<StripeInvoice> GetInvoices(int offset, int count, string customerId, out int total)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset");
            if (count < 1 || count > 100) throw new ArgumentOutOfRangeException("count");

            var str = new StringBuilder();
            str.AppendFormat("offset={0}&", offset);
            str.AppendFormat("count={0}&", count);
            if (!string.IsNullOrEmpty(customerId)) str.AppendFormat("customer={0}&", HttpUtility.UrlEncode(customerId));

            str.Length--;
            string ep = String.Format("{0}/invoices?{1}", api_endpoint, str);
            string json = DoRequest(ep);
            var invoiceItems = JsonConvert.DeserializeObject<StripeInvoiceCollection>(json);
            total = invoiceItems.Total;
            return invoiceItems.Invoices;
        }

        public StripeInvoice GetUpcomingInvoice(string customerId)
        {
            if (string.IsNullOrEmpty(customerId)) throw new ArgumentOutOfRangeException("customerId");
            string ep = String.Format("{0}/invoices/upcoming?customer={1}", api_endpoint, customerId);
            string json = DoRequest(ep);
            return JsonConvert.DeserializeObject<StripeInvoice>(json);
        }

        #endregion

        #region Coupons

        public StripeCoupon CreateCoupon(StripeCouponInfo coupon)
        {
            if (coupon == null) throw new ArgumentNullException("coupon");
            if (coupon.PercentOff < 1 || coupon.PercentOff > 100) throw new ArgumentOutOfRangeException("coupon.PercentOff");
            if (coupon.Duration == StripeCouponDuration.Repeating && coupon.MonthsForDuration < 1) throw new ArgumentException("MonthsForDuration must be greater than 1 when Duration = Repeating");
            StringBuilder str = UrlEncode(coupon);
            string ep = string.Format("{0}/coupons", api_endpoint);
            string json = DoRequest(ep, "POST", str.ToString());
            return JsonConvert.DeserializeObject<StripeCoupon>(json);
        }

        public StripeCoupon GetCoupon(string couponId)
        {
            if (string.IsNullOrEmpty(couponId)) throw new ArgumentNullException("couponId");
            string ep = string.Format("{0}/coupons/{1}", api_endpoint, couponId);
            string json = DoRequest(ep);
            return JsonConvert.DeserializeObject<StripeCoupon>(json);
        }

        public StripeCoupon DeleteCoupon(string couponId)
        {
            if (string.IsNullOrEmpty(couponId)) throw new ArgumentNullException("couponId");
            string ep = string.Format("{0}/coupons/{1}", api_endpoint, couponId);
            string json = DoRequest(ep, "DELETE", null);
            return JsonConvert.DeserializeObject<StripeCoupon>(json);
        }

        public List<StripeCoupon> GetCoupons()
        {
            int dummy;
            return GetCoupons(0, 10, out dummy);
        }

        public List<StripeCoupon> GetCoupons(int offset, int count, out int total)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset");
            if (count > 100) throw new ArgumentOutOfRangeException("count");
            string ep = string.Format("{0}/coupons?offset={0}&count={1}", api_endpoint, offset, count);
            string json = DoRequest(ep);
            StripeCouponCollection coupons = JsonConvert.DeserializeObject<StripeCouponCollection>(json);
            total = coupons.Total;
            return coupons.Coupons;
        }

        #endregion

        public int TimeoutSeconds { get; set; }
    }
}
