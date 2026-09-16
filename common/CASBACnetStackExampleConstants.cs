// SPDX-License-Identifier: CC0-1.0
// Public-domain example code (CC0) - see ../LICENSE.

// CASBACnetStackExampleConstants.cs
// =============================================================================
// A small, self-contained set of the BACnet enumeration values this example
// project needs. The CAS BACnet Stack defines the FULL enumerations inside
// submodules/cas-bacnet-stack/adapters/csharp/CASBACnetStackAdapter.cs (the
// "Enumerations" region) - this file only adds the handful of names that
// adapter does NOT already define (mostly because that adapter is generated
// from the stack's C++/TypeScript enumerations and has not grown every
// BACnet enumeration yet). Where the adapter already has an equivalent
// constant we reuse it directly from CASBACnetStack.CASBACnetStackAdapter
// instead of duplicating it here - see the comment on each block below.
//
// Every value matches the BACnet standard (ANSI/ASHRAE 135) and the CAS
// BACnet Stack enumerations, and every constant name matches the C++/Node
// editions of this file (common/CASBACnetStackExampleConstants.h in
// BACnetProfileExample-B-SS-CPP, common/CASBACnetStackExampleConstants.ts in
// BACnetProfileExample-B-SS-Node) so the three languages diff 1:1. Add more
// as your own project needs them.
// =============================================================================

using System;

namespace BACnetProfileExampleBSSCS.Common
{
    public static class CASBACnetStackExampleConstants
    {
        // -- BACnet object types (Object_Type enumeration) ----------------------
        //    Already in the adapter as CASBACnetStackAdapter.OBJECT_TYPE_* -
        //    reused directly in Program.cs. Nothing to add here.

        // -- BACnet property identifiers (Property_Identifier enumeration) ------
        //    Already in the adapter as CASBACnetStackAdapter.PROPERTY_IDENTIFIER_*
        //    (note: the adapter spells them without underscores, e.g.
        //    PROPERTY_IDENTIFIER_OBJECT_NAME - same values as the C++/Node
        //    editions' PROPERTY_IDENTIFIER_OBJECT_NAME). Reused directly.

        // -- BACnet engineering units (Engineering_Units enumeration) -----------
        //    Full list: submodules/cas-bacnet-stack/source/BACnetEngineeringUnits.h
        //    Not in the adapter's enumeration block - defined here.
        public const UInt32 ENGINEERING_UNITS_DEGREES_CELSIUS = 62;

        // -- BACnet polarity (Polarity enumeration, for Binary objects) ---------
        //    Full list: submodules/cas-bacnet-stack/source/BACnetPolarity.h
        //    Not in the adapter's enumeration block - defined here.
        public const UInt32 POLARITY_NORMAL = 0;

        // -- BACnet/IP mode (BACnetIPMode enumeration, for the Network Port) ----
        //    Full list: submodules/cas-bacnet-stack/source/BACnetIPMode.h
        //    Not in the adapter's enumeration block - defined here.
        public const UInt32 BACNET_IP_MODE_NORMAL = 0;

        // -- BACnet services (Services_Supported enumeration) -------------------
        //    Used with BACnetStack_SetServiceEnabled() to turn individual
        //    services on/off. The adapter defines these under "Services
        //    Supported enumeration" as SERVICES_SUPPORTED_* (same values):
        //    SERVICES_SUPPORTED_READ_PROPERTY = 12, SERVICES_SUPPORTED_WHO_IS = 34,
        //    SERVICES_SUPPORTED_I_AM = 26, SERVICES_SUPPORTED_WHO_HAS = 33,
        //    SERVICES_SUPPORTED_I_HAVE = 27. Reused directly (as
        //    CASBACnetStackAdapter.SERVICES_SUPPORTED_READ_PROPERTY, etc.) rather
        //    than re-declared here.

        // -- Network Port object network type (BACnetNetworkType enumeration,
        //    used by BACnetStack_AddNetworkPortObject). The adapter defines this
        //    as NETWORK_PORT_OBJECT_NETWORK_TYPE_IPV4 = 5 (same value as the
        //    C++/Node editions' NETWORK_PORT_NETWORK_TYPE_IPV4). Reused directly.

        // -- Network Port object protocol level (BACnetProtocolLevel enumeration,
        //    used by BACnetStack_AddNetworkPortObject). The adapter defines this
        //    as PROTOCOL_LEVEL_BACNET_APPLICATION = 2 (same value as the
        //    C++/Node editions' NETWORK_PORT_PROTOCOL_LEVEL_BACNET_APPLICATION).
        //    Reused directly.

        // The lowest protocol layer references this sentinel instead of another
        // port. Not in the adapter's enumeration block - defined here (matches
        // the adapter's own NETWORK_PORT_LOWEST_PROTOCOL_LAYER constant, which
        // is the same value under a different name).
        public const UInt32 NETWORK_PORT_REFERENCE_PORT_NONE = 4194303;

        // -- Network_Number_Quality (BACnetNetworkNumberQuality, cl. 12.56.11).
        //    Says how the port learned its Network_Number. A port that has not
        //    been told and has not learned one reports "unknown" with
        //    Network_Number = 0. The adapter defines this as
        //    NETWORK_NUMBER_QUALITY_UNKNOWN = 0. Reused directly.

        // -- Character string encoding (the encoding byte written by the
        //    character-string Get callback). 0 = UTF-8. The BACnet character-set
        //    values are defined by ANSI/ASHRAE 135 Clause 20.2.9. Not in the
        //    adapter's enumeration block - defined here.
        public const Byte CHARACTER_STRING_ENCODING_UTF8 = 0;

        // -- BACnet error codes (Error_Code enumeration) -------------------------
        //    Full list: submodules/cas-bacnet-stack/source/BACnetErrorCode.h
        //    A GetProperty* callback (stack issue #974) may write one of these to
        //    its trailing errorCode out-param and return false to name the
        //    BACnet error a client receives, instead of letting the stack
        //    silently substitute a default. This example uses it in exactly one
        //    place: an out-of-range State_Text array index. The adapter defines
        //    this as ERROR_INVALID_ARRAY_INDEX = 42 (same value, different
        //    spelling than the C++/Node editions' ERROR_CODE_INVALID_ARRAY_INDEX).
        //    Reused directly.
    }
}
