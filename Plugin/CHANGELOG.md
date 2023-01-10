# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.3.8] - 2022-12-23

### Fixed

- Invoices were not generated after upgrade to BTCPay Server v1.7.2. (btcpayserver/btcpayserver#4458)

### Changed

- Updates for BTCPay Server v1.7.3

## [1.3.7] - 2022-12-19

### Added

- Parse payment URLs.

### Fixed

- Allow invalidating pending invoices. (dennisreimann/btcpayserver-plugin-lnbank#23)

## [1.3.6] - 2022-12-15

### Changed

- Updates for BTCPay Server v1.7.2

## [1.3.5] - 2022-09-30

### Changed

- Date display improvements.

## [1.3.4] - 2022-09-30

### Changed

- Improve LNURL payment flow.
- Improve invoice canceling and invalidating.
- Minor Send and Receive view improvements.

## [1.3.3] - 2022-09-29

### Changed

- Handle invalid transactions in background watcher.

### Fixed

- LNDhub API: Fix send error.
- Lightning setup: Fix setting LNbank wallet in WebKit-based browsers. (btcpayserver/btcpayserver-plugins#44)

## [1.3.2] - 2022-09-27

### Added

- Invoices API for BTCPay Lightning client. (btcpayserver/BTCPayServer.Lightning#99)

### Fixed

- Fix missing icon in sidebar.
- LNDhub-API: Fix invoices list. (btcpayserver/btcpayserver#4168)

## [1.3.1] - 2022-09-26

### Fixed

- Fix LNURL metadata. (btcpayserver/btcpayserver#4165)
- Fix migration. (dennisreimann/btcpayserver#22)

## [1.3.0] - 2022-09-26

### Added

- LNDhub-compatible API: Wallets are usable with BlueWallet, Zeus and Alby.
- Wallet access keys: Share wallet access, supporting different access levels.
- Send: LNURL-Pay and Lightning Address support.
- Receive: Add custom invoice expiry as advanced option.

### Changed

- Handle expired invoices in background watcher.
- More logging in background watcher.

## [1.2.3] - 2022-07-08

### Changed

- Improve send error handling.
- Improve API responses.
- Allow creation of zero amount invoices.

## [1.2.2] - 2022-05-30

### Added

- Public wallet LNURL page for sharing.

### Changed

- Distinguish original invoice amount and actual amount settled.
- Improve hold invoice handling.

### Fixed

- Allow specifying explicit amount for zero amount invoices.

## [1.2.1] - 2022-04-30

### Added

- Refresh transactions list on update.
- Log exceptions in background watcher.
- Handling for hold invoices.
- Autofocus input fields.

### Fixed

- Allow for empty description when creating invoices.
- Handle cancelled invoices in background watcher.

## [1.2.0] - 2022-04-01

### Added

- LNURL-Pay for receiving transactions.
- API for accessing, updating and deleting LNbank wallets.
- Export wallet transactions for accounting (CSV and JSON).

## [1.1.1] - 2022-03-09

### Added

- API for creating LNbank wallets.

### Changed

- Use store invoice expiry time.
- Soft delete wallets (only mark as deleted).

### Fixed

- Websocket connection to update transaction states.
- Handle crashes in background service.
- Fix redirects.

## [1.1.0] - 2022-02-21

### Added

- Toggle for attaching description to pay request when receiving.
- Allow for empty description when receiving.
- Customize description when sending.
- Prevent deletion of wallet with balance.

### Changed

- Proper redirects on homepage (create wallet if none exists).
- Separate wallet list and wallet details views.
- Common wallet header for all views.

### Fixed

- Fee handling: User pays routing fee and needs to have a fee reserve when sending.
- Prevent paying payment requests multiple times.

## [1.0.4] - 2022-02-10

### Added

- Support for private route hints: Will be enabled if the connected store has the required setting or if the toggle on
  the receive page is activated.

### Changed

- Lowercase page paths.
- Remove button icons, improve wallet view.

### Fixed

- Logo link on Share page.

## [1.0.3] - 2022-02-01

### Added

- Form validation

### Changed

- Improve create wallet for LN node connection case.

### Fixed

- Non-admins cannot send and receive when using the internal node.
