# M4-positive-collisions 视觉真值

## 权威输入

- DWG：`regression/visual-inspection/M4-positive-collisions.dwg`
- SHA-256：`79D944E913627459872C6D94F120198E0FBD92105E77A0D409CEF8C9A3ECDD88`
- 模式：`WarningOnly`（`blocking=false`）

## Debug-v3 DLL

- `AutoFixtureDim.dll`：`B145FDCCC2C5DFA05FCBEA5E8886BC3AD27B4A577D0366EFFD2762A53990CBED`
- `CadAuto.Core.dll`：`4D20C161DF40BFB0C5B67C6D498438D42F9EBC7EA5B2FCA1BB1CFD0BA34E9498`
- `CadAuto.CadAdapter.dll`：`D3961973C6F6152789B969BCFF9EC607803B6602D29D774B4CB959DC03CC6857`

## 已确认真值

- 顶部：`TEXT_TEXT_COLLISION`（handles：`ED7303`、`ED7304`、`ED7331`、`ED733A`）是明确的文字重叠缺陷。可将尺寸 `10±0.05` 置于尺寸线左侧。
- 左侧 `20 / 10±0.05`：低优先级布局缺陷；当前检查器为 `detected=false`、`manualReviewRequired=true`。在不影响其他尺寸文字时，可将尺寸 `10±0.05` 置于尺寸线下方。

## 理想参考

- 图：`regression/visual-inspection/runs/M4-positive-collisions-real/20260731-104632461/ideal-reference.png`
- SHA-256：`469D1B25F906A29C651544FE842AF8F8B82F4FEC51B37B36BC15011AE3817DF0`

## 边界

本记录仅归档用户确认的视觉真值；不更新产品、expectation、fixture 或 manifest。
