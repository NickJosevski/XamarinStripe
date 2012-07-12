/*
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
    public interface IStripePayment
    {
        StripeCharge Charge(int amountInCents, string currency, string customer, string description);

        StripeCharge Charge(int amountInCents, string currency, StripeCreditCardInfo card, string description);

        StripeCharge GetCharge(string chargeId);

        List<StripeCharge> GetCharges();

        List<StripeCharge> GetCharges(int offset, int count);

        List<StripeCharge> GetCharges(int offset, int count, string customerId);

        List<StripeCharge> GetCharges(int offset, int count, string customerId, out int total);

        StripeCharge Refund(string chargeId);

        StripeCharge Refund(string chargeId, int amount);

        StripeCustomer CreateCustomer(StripeCustomerInfo customer);

        StripeCustomer CreateCustomer(StripeCustomerTokenInfo customer);

        StripeCustomer UpdateCustomer(string id, StripeCustomerInfo customer);

        StripeCustomer GetCustomer(string customerId);

        List<StripeCustomer> GetCustomers();

        List<StripeCustomer> GetCustomers(int offset, int count);

        List<StripeCustomer> GetCustomers(int offset, int count, out int total);

        StripeCustomer DeleteCustomer(string customerId);

        StripeCreditCardToken CreateToken(StripeCreditCardInfo card);

        StripeCreditCardToken GetToken(string tokenId);

        StripePlan CreatePlan(StripePlanInfo plan);

        bool PlanExists(string planId);

        StripePlan GetPlan(string planId);

        StripePlan DeletePlan(string planId);

        List<StripePlan> GetPlans();

        List<StripePlan> GetPlans(int offset, int count);

        List<StripePlan> GetPlans(int offset, int count, out int total);

        StripeSubscription Subscribe(string customerId, StripeSubscriptionInfo subscription);

        StripeSubscription GetSubscription(string customerId);

        StripeSubscription Unsubscribe(string customerId, bool atPeriodEnd);

        StripeInvoiceItem CreateInvoiceItem(StripeInvoiceItemInfo item);

        StripeInvoiceItem GetInvoiceItem(string invoiceItemId);

        StripeInvoiceItem UpdateInvoiceItem(string invoiceItemId, StripeInvoiceItemUpdateInfo item);

        StripeInvoiceItem DeleteInvoiceItem(string invoiceItemId);

        List<StripeInvoiceItem> GetInvoiceItems();

        List<StripeInvoiceItem> GetInvoiceItems(int offset, int count);

        List<StripeInvoiceItem> GetInvoiceItems(int offset, int count, string customerId);

        List<StripeInvoiceItem> GetInvoiceItems(int offset, int count, string customerId, out int total);

        StripeInvoice GetInvoice(string invoiceId);

        List<StripeInvoice> GetInvoices();

        List<StripeInvoice> GetInvoices(int offset, int count);

        List<StripeInvoice> GetInvoices(int offset, int count, string customerId);

        List<StripeInvoice> GetInvoices(int offset, int count, string customerId, out int total);

        StripeInvoice GetUpcomingInvoice(string customerId);

        StripeCoupon CreateCoupon(StripeCouponInfo coupon);

        StripeCoupon GetCoupon(string couponId);

        StripeCoupon DeleteCoupon(string couponId);

        List<StripeCoupon> GetCoupons();

        List<StripeCoupon> GetCoupons(int offset, int count, out int total);
    }

    public class StripePayment : IStripePayment
    {
        private const string ApiEndpoint = "https://api.stripe.com/v1";

        private const string SubscriptionPath = "{0}/customers/{1}/subscription";

        private const string UserAgent = "Stripe .NET v1";

        private static readonly Encoding Encoding = Encoding.UTF8;

        private readonly ICredentials _credential;

        public StripePayment(string apiKey)
        {
            _credential = new NetworkCredential(apiKey, "");
            TimeoutSeconds = 30;
        }

        #region Shared

        protected virtual WebRequest SetupRequest(string method, string url)
        {
            var req = WebRequest.Create(url);
            req.Method = method;
            if (req is HttpWebRequest)
            {
                ((HttpWebRequest)req).UserAgent = UserAgent;
            }
            req.Credentials = _credential;
            req.PreAuthenticate = true;
            req.Timeout = TimeoutSeconds * 1000;
            if (method == "POST") req.ContentType = "application/x-www-form-urlencoded";
            return req;
        }

        private static string GetResponseAsString(WebResponse response)
        {
            using (var sr = new StreamReader(response.GetResponseStream(), Encoding))
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
            var webRequest = SetupRequest(method, endpoint);
            if (body != null)
            {
                var bytes = Encoding.GetBytes(body);
                webRequest.ContentLength = bytes.Length;
                using (var st = webRequest.GetRequestStream())
                {
                    st.Write(bytes, 0, bytes.Length);
                }
            }

            try
            {
                using (var resp = webRequest.GetResponse())
                {
                    result = GetResponseAsString(resp);
                }
            }
            catch (WebException wexc)
            {
                if (wexc.Response != null)
                {
                    var jsonError = GetResponseAsString(wexc.Response);
                    var statusCode = HttpStatusCode.BadRequest;
                    var resp = wexc.Response as HttpWebResponse;
                    if (resp != null) statusCode = resp.StatusCode;

                    if ((int)statusCode <= 500) throw StripeException.GetFromJson(statusCode, jsonError);
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

        public StripeCharge Charge(int amountInCents, string currency, string customer, string description)
        {
            if (string.IsNullOrEmpty(customer)) throw new ArgumentNullException("customer");

            return Charge(amountInCents, currency, customer, null, description);
        }

        public StripeCharge Charge(int amountInCents, string currency, StripeCreditCardInfo card, string description)
        {
            if (card == null) throw new ArgumentNullException("card");

            return Charge(amountInCents, currency, null, card, description);
        }

        private StripeCharge Charge(int amountInCents, string currency, string customer, StripeCreditCardInfo card, string description)
        {
            if (amountInCents < 0) throw new ArgumentOutOfRangeException("amountInCents", "Must be greater than or equal 0");
            if (string.IsNullOrEmpty(currency)) throw new ArgumentNullException("currency");
            currency = currency.ToLower();
            if (currency != "usd") throw new ArgumentException("The only supported currency is 'usd'");

            var str = new StringBuilder();
            str.AppendFormat("amount={0}&", amountInCents);
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
            var ep = string.Format("{0}/charges", ApiEndpoint);
            var json = DoRequest(ep, "POST", str.ToString());
            return JsonConvert.DeserializeObject<StripeCharge>(json);
        }

        public StripeCharge GetCharge(string chargeId)
        {
            if (string.IsNullOrEmpty(chargeId)) throw new ArgumentNullException("chargeId");

            var ep = string.Format("{0}/charges/{1}", ApiEndpoint, HttpUtility.UrlEncode(chargeId));
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

        public List<StripeCharge> GetCharges(int offset, int count, string customerId)
        {
            int dummy;
            return GetCharges(offset, count, customerId, out dummy);
        }

        public List<StripeCharge> GetCharges(int offset, int count, string customerId, out int total)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset");
            if (count < 1 || count > 100) throw new ArgumentOutOfRangeException("count");

            var str = new StringBuilder();
            str.AppendFormat("offset={0}&", offset);
            str.AppendFormat("count={0}&", count);
            if (!string.IsNullOrEmpty(customerId)) str.AppendFormat("customer={0}&", HttpUtility.UrlEncode(customerId));

            str.Length--;
            var ep = string.Format("{0}/charges?{1}", ApiEndpoint, str);
            var json = DoRequest(ep);
            try
            {
                var charges = JsonConvert.DeserializeObject<StripeChargeCollection>(json);
                total = charges.Total;
                return charges.Charges;
            }
            catch (Exception ex)
            {
                var jsonDeserialzeEx = ex.Message;
                throw;
            }
        }

        #endregion

        #region Refund

        public StripeCharge Refund(string chargeId)
        {
            if (string.IsNullOrEmpty(chargeId)) throw new ArgumentNullException("chargeId");

            var ep = string.Format("{0}/charges/{1}/refund", ApiEndpoint, HttpUtility.UrlEncode(chargeId));
            var json = DoRequest(ep, "POST", null);
            return JsonConvert.DeserializeObject<StripeCharge>(json);
        }

        public StripeCharge Refund(string chargeId, int amount)
        {
            if (string.IsNullOrEmpty(chargeId)) throw new ArgumentNullException("chargeId");
            if (amount <= 0) throw new ArgumentException("Amount must be greater than zero.", "amount");

            var ep = string.Format(
                "{0}/charges/{1}/refund?amount={2}", ApiEndpoint, HttpUtility.UrlEncode(chargeId), amount);
            var json = DoRequest(ep, "POST", null);
            return JsonConvert.DeserializeObject<StripeCharge>(json);
        }

        #endregion

        #region Customer

        private StripeCustomer CreateOrUpdateCustomer(string id, StripeCustomerInfo customer)
        {
            var str = UrlEncode(customer);

            var format = "{0}/customers"; // Create
            if (id != null) format = "{0}/customers/{1}"; // Update
            var ep = string.Format(format, ApiEndpoint, HttpUtility.UrlEncode(id));
            var json = DoRequest(ep, "POST", str.ToString());
            return JsonConvert.DeserializeObject<StripeCustomer>(json);
        }

        private StripeCustomer CreateCustomerWithToken(string id, StripeCustomerTokenInfo customer)
        {
            var str = UrlEncode(customer);

            var format = "{0}/customers"; // Create
            if (id != null) format = "{0}/customers/{1}"; // Update
            var ep = string.Format(format, ApiEndpoint, HttpUtility.UrlEncode(id));
            var json = DoRequest(ep, "POST", str.ToString());
            return JsonConvert.DeserializeObject<StripeCustomer>(json);
        }

        public StripeCustomer CreateCustomer(StripeCustomerTokenInfo customer)
        {
            if (customer == null) throw new ArgumentNullException("customer");

            return CreateCustomerWithToken(null, customer);
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

        public StripeCustomer GetCustomer(string customerId)
        {
            if (string.IsNullOrEmpty(customerId)) throw new ArgumentNullException("customerId");

            var ep = string.Format("{0}/customers/{1}", ApiEndpoint, HttpUtility.UrlEncode(customerId));
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

            var str = string.Format("offset={0}&count={1}", offset, count);
            var ep = string.Format("{0}/customers?{1}", ApiEndpoint, str);
            var json = DoRequest(ep);
            var customers = JsonConvert.DeserializeObject<StripeCustomerCollection>(json);
            total = customers.Total;
            return customers.Customers;
        }

        public StripeCustomer DeleteCustomer(string customerId)
        {
            if (string.IsNullOrEmpty(customerId)) throw new ArgumentNullException("customerId");

            var ep = string.Format("{0}/customers/{1}", ApiEndpoint, HttpUtility.UrlEncode(customerId));
            var json = DoRequest(ep, "DELETE", null);
            return JsonConvert.DeserializeObject<StripeCustomer>(json);
        }

        #endregion

        #region Tokens

        public StripeCreditCardToken CreateToken(StripeCreditCardInfo card)
        {
            if (card == null) throw new ArgumentNullException("card");
            StringBuilder str = UrlEncode(card);

            var ep = string.Format("{0}/tokens", ApiEndpoint);
            var json = DoRequest(ep, "POST", str.ToString());
            return JsonConvert.DeserializeObject<StripeCreditCardToken>(json);
        }

        public StripeCreditCardToken GetToken(string tokenId)
        {
            if (string.IsNullOrEmpty(tokenId)) throw new ArgumentNullException(tokenId);

            var ep = string.Format("{0}/tokens/{1}", ApiEndpoint, HttpUtility.UrlEncode(tokenId));
            var json = DoRequest(ep);
            return JsonConvert.DeserializeObject<StripeCreditCardToken>(json);
        }

        #endregion

        #region Plans

        public StripePlan CreatePlan(StripePlanInfo plan)
        {
            if (plan == null) throw new ArgumentNullException("plan");
            var str = UrlEncode(plan);

            var ep = string.Format("{0}/plans", ApiEndpoint);
            var json = DoRequest(ep, "POST", str.ToString());
            return JsonConvert.DeserializeObject<StripePlan>(json);
        }

        public bool PlanExists(string planId)
        {
            try
            {
                if (string.IsNullOrEmpty(planId)) throw new ArgumentNullException("planId");

                var ep = string.Format("{0}/plans/{1}", ApiEndpoint, HttpUtility.UrlEncode(planId));
                var json = DoRequest(ep);
                var result = JsonConvert.DeserializeObject<StripePlan>(json);
                return result.Id == planId;
            }
            catch (StripeException exception)
            {
                var se = exception.StripeError;
                if (se.Message.Contains("No such plan") && se.ErrorType == "invalid_request_error") return false;

                throw;
            }
        }

        public StripePlan GetPlan(string planId)
        {
            if (string.IsNullOrEmpty(planId)) throw new ArgumentNullException("planId");

            var ep = string.Format("{0}/plans/{1}", ApiEndpoint, HttpUtility.UrlEncode(planId));
            var json = DoRequest(ep);
            return JsonConvert.DeserializeObject<StripePlan>(json);
        }

        public StripePlan DeletePlan(string planId)
        {
            if (string.IsNullOrEmpty(planId)) throw new ArgumentNullException("planId");

            var ep = string.Format("{0}/plans/{1}", ApiEndpoint, HttpUtility.UrlEncode(planId));
            var json = DoRequest(ep, "DELETE", null);
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
            var ep = string.Format("{0}/plans?{1}", ApiEndpoint, str);
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
            var ep = string.Format(SubscriptionPath, ApiEndpoint, HttpUtility.UrlEncode(customerId));
            var json = DoRequest(ep, "POST", str.ToString());
            return JsonConvert.DeserializeObject<StripeSubscription>(json);
        }

        public StripeSubscription GetSubscription(string customerId)
        {
            if (string.IsNullOrEmpty(customerId)) throw new ArgumentNullException("customerId");
            var ep = string.Format(SubscriptionPath, ApiEndpoint, HttpUtility.UrlEncode(customerId));
            var json = DoRequest(ep);
            return JsonConvert.DeserializeObject<StripeSubscription>(json);
        }

        public StripeSubscription Unsubscribe(string customerId, bool atPeriodEnd)
        {
            var ep = string.Format(
                SubscriptionPath + "?at_period_end={2}",
                ApiEndpoint,
                HttpUtility.UrlEncode(customerId),
                atPeriodEnd.ToString(CultureInfo.InvariantCulture).ToLowerInvariant());
            var json = DoRequest(ep, "DELETE", null);
            return JsonConvert.DeserializeObject<StripeSubscription>(json);
        }

        #endregion

        #region Invoice items

        public StripeInvoiceItem CreateInvoiceItem(StripeInvoiceItemInfo item)
        {
            if (string.IsNullOrEmpty(item.CustomerId)) throw new ArgumentNullException("item.CustomerId");
            var str = UrlEncode(item);
            var ep = string.Format("{0}/invoiceitems", ApiEndpoint);
            var json = DoRequest(ep, "POST", str.ToString());
            return JsonConvert.DeserializeObject<StripeInvoiceItem>(json);
        }

        public StripeInvoiceItem GetInvoiceItem(string invoiceItemId)
        {
            if (string.IsNullOrEmpty(invoiceItemId)) throw new ArgumentNullException("invoiceItemId");
            var ep = string.Format("{0}/invoiceitems/{1}", ApiEndpoint, invoiceItemId);
            var json = DoRequest(ep);
            return JsonConvert.DeserializeObject<StripeInvoiceItem>(json);
        }

        public StripeInvoiceItem UpdateInvoiceItem(string invoiceItemId, StripeInvoiceItemUpdateInfo item)
        {
            var str = UrlEncode(item);
            var ep = string.Format("{0}/invoiceitems/{1}", ApiEndpoint, invoiceItemId);
            var json = DoRequest(ep, "POST", str.ToString());
            return JsonConvert.DeserializeObject<StripeInvoiceItem>(json);
        }

        public StripeInvoiceItem DeleteInvoiceItem(string invoiceItemId)
        {
            var ep = string.Format("{0}/invoiceitems/{1}", ApiEndpoint, invoiceItemId);
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
            var ep = string.Format("{0}/invoiceitems?{1}", ApiEndpoint, str);
            var json = DoRequest(ep);
            var invoiceItems = JsonConvert.DeserializeObject<StripeInvoiceItemCollection>(json);
            total = invoiceItems.Total;
            return invoiceItems.InvoiceItems;
        }

        #endregion

        #region Invoices

        public StripeInvoice GetInvoice(string invoiceId)
        {
            if (string.IsNullOrEmpty(invoiceId)) throw new ArgumentNullException("invoiceId");
            var ep = string.Format("{0}/invoices/{1}", ApiEndpoint, invoiceId);
            var json = DoRequest(ep);
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
            var ep = string.Format("{0}/invoices?{1}", ApiEndpoint, str);
            var json = DoRequest(ep);
            var invoiceItems = JsonConvert.DeserializeObject<StripeInvoiceCollection>(json);
            total = invoiceItems.Total;
            return invoiceItems.Invoices;
        }

        public StripeInvoice GetUpcomingInvoice(string customerId)
        {
            if (string.IsNullOrEmpty(customerId)) throw new ArgumentOutOfRangeException("customerId");
            var ep = string.Format("{0}/invoices/upcoming?customer={1}", ApiEndpoint, customerId);
            var json = DoRequest(ep);
            return JsonConvert.DeserializeObject<StripeInvoice>(json);
        }

        #endregion

        #region Coupons

        public StripeCoupon CreateCoupon(StripeCouponInfo coupon)
        {
            if (coupon == null) throw new ArgumentNullException("coupon");
            if (coupon.PercentOff < 1 || coupon.PercentOff > 100) throw new ArgumentOutOfRangeException("coupon.PercentOff");
            if (coupon.Duration == StripeCouponDuration.repeating && coupon.MonthsForDuration < 1) throw new ArgumentException("MonthsForDuration must be greater than 1 when Duration = Repeating");
            var str = UrlEncode(coupon);
            var ep = string.Format("{0}/coupons", ApiEndpoint);
            var json = DoRequest(ep, "POST", str.ToString());
            return JsonConvert.DeserializeObject<StripeCoupon>(json);
        }

        public StripeCoupon GetCoupon(string couponId)
        {
            if (string.IsNullOrEmpty(couponId)) throw new ArgumentNullException("couponId");
            var ep = string.Format("{0}/coupons/{1}", ApiEndpoint, couponId);
            var json = DoRequest(ep);
            return JsonConvert.DeserializeObject<StripeCoupon>(json);
        }

        public StripeCoupon DeleteCoupon(string couponId)
        {
            if (string.IsNullOrEmpty(couponId)) throw new ArgumentNullException("couponId");
            var ep = string.Format("{0}/coupons/{1}", ApiEndpoint, couponId);
            var json = DoRequest(ep, "DELETE", null);
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
            var ep = string.Format("{0}/coupons?offset={0}&count={1}", ApiEndpoint, offset, count);
            var json = DoRequest(ep);
            var coupons = JsonConvert.DeserializeObject<StripeCouponCollection>(json);
            total = coupons.Total;
            return coupons.Coupons;
        }

        #endregion

        public int TimeoutSeconds { get; set; }
    }
}
