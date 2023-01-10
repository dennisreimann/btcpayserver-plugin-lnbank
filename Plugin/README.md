# LNbank

A plugin for [BTCPay Server](https://github.com/btcpayserver) to use the internal Lightning node in custodial mode:
It allows server admins to open up the Lightning node and give users access via custodial layer 3 wallets.
Users can create separate Lightning wallets and use them to send and receive Lightning payments.

## Use cases

* Allow non-admin users to use the internal Lightning node.
* "Uncle Jim" mode: Give access to your friends and family.
* Use individual LNbank wallets for stores and separate the Lightning accounting.
* Use LNbank wallets individually, without having them tied to a store.
* Share access to LNbank wallets between multiple users with different access levels.

## Technicalities

* The LNbank accounts are separated on a database level, not on the layer 2/Lightning implementation level.
  LNbank wallets can be seen as layer 3 sub-accounts.
* All LNbank accounts use the internal Lightning node and share the Node ID of that node.
* Channels and liquidity are managed by the server admin.

## Caveats

Users rely on the server admin as the custodian, be aware of that trust relationship.
When using LNbank on a third-party instance whose owner you don't know, mitigate the risks by following this advise:

* Keep only small amount in the LNbank wallets.
* Regularly transfer funds to a Lightning node or account owned by yourself.
* Switch to an own BTCPay Server instance once you start receiving larger payments.

## Features and Compatibility

* Send to BOLT11 payment requests, as well as LNURL and Lightning Address.
* LNbank offers a [LNDhub-compatible](./docs/LNDhub.md) API, wallets are usable with BlueWallet, Zeus and Alby.
* Use the Greenfield API to create and manage LNbank wallets.

## How to activate and use LNbank

### Server admin

* LNbank has to be installed and activated by the server admin via the Plugins menu.
* When activated, LNbank is available to users regardless of the "Allow non-admins to use the internal lightning node in
  their stores" setting in `Server Settings > Policies`.
* Requirements: BTCPay Server v1.6

### User

* Each user can create an unlimited number of LNbank wallets.
* Wallet overview: See your balance and list of transactions with details like payment state and fees.
* Receive: Specify an amount and description, either for your accounting only or also attach it to the payment request.
* Send: Decode payment requests (BOLT11, LNURL, Lightning Address) and confirm the payment.
* Settings: See and edit the wallet details and give access to other users via access keys.
* To remove a LNbank wallet, it must be emptied out first and have no balance left.
* Connect your LNbank wallet to a store via the store's Lightning node setup page. (see the "Use LNbank wallet" option)
