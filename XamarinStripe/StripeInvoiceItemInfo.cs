/*
 * Copyright 2011 Joe Dluzen
 *
 * Author(s):
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
using System.Text;
using System.Web;

namespace Xamarin.Payments.Stripe
{
    public class StripeInvoiceItemInfo : IUrlEncoderInfo
    {
        public string CustomerId { get; set; }

        public int Amount { get; set; }

        public string Currency { get; set; }

        public string Description { get; set; }

        public virtual void UrlEncode(StringBuilder sb)
        {
            sb.AppendFormat(
                "customer={0}&amount={1}&currency={2}&",
                HttpUtility.UrlEncode(CustomerId),
                Amount,
                HttpUtility.UrlEncode(Currency ?? "usd"));

            if (!string.IsNullOrEmpty(Description)) sb.AppendFormat("description={0}&", HttpUtility.UrlEncode(Description));
        }
    }

    public class StripeInvoiceItemUpdateInfo : IUrlEncoderInfo
    {
        public string CustomerId { get; set; }

        public int Amount { get; set; }

        public string Currency { get; set; }

        public string Description { get; set; }

        public virtual void UrlEncode(StringBuilder sb)
        {
            if (!string.IsNullOrEmpty(Currency))
                throw new ArgumentException("Cannot change currency via InvoiceItemUpdate");

            if (!string.IsNullOrEmpty(CustomerId))
                throw new ArgumentException("CustomerId should not be supplied when performing InvoiceItemUpdate");

            sb.AppendFormat(
                "amount={0}&",
                Amount);

            if (!string.IsNullOrEmpty(Description)) sb.AppendFormat("description={0}&", HttpUtility.UrlEncode(Description));
        }
    }
}
