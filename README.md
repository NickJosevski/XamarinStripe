XamarinStripe provides .NET bindings to process online payments with Stripe.com.

	XamarinStripe/

		Source code for the assembly.

		
### Modifications on this fork

1.  The sample program using the PaymentTest/ library has been morphed into NUnit tests
	
	Tests.XamarinStripe/
	
2.  Some additional checks have been added to some Stripe*Info classes that inherit from IUrlEncoderInfo
	
3. JSON deserialization relevant changes have been made to classes that are created as part of deserialization including public enums now matching the case and format of the API.
	
4. Create Customer overload taking a single item to match API, e.g. 'card=tok_S1ngl3UseT0k3n' via 'StripeCustomerTokenInfo'

5.  It's strongly named, this is how we need it.
   
   
   
   
Excuse the bloat of this repositor, since I plan to extract out only the stripe logic later, the bloated all inclusive asp.net mvc test app has made this larger than it should be (no package ignoring happening to simplify things).
	

## Licencse		
		
Copyright 2011 Joe Dluzen

Author(s):
 Joe Dluzen (jdluzen@gmail.com)

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

