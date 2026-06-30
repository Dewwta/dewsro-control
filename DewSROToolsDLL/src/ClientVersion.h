#pragma once
#include <cstdint>

// Must match Constant.COMPAT_CLIENT on the proxy API side.
// Format: major * 100 + minor  (100 = v1.00, 105 = v1.05, 200 = v2.00)
static constexpr uint16_t DLL_COMPAT_VERSION = 102;
