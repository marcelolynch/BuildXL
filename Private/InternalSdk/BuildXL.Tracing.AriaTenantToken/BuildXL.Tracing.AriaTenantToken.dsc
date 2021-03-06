// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import {Transformer} from "Sdk.Transformers";

// This is an empty facade for a Microsoft internal package.

namespace Contents {
    export declare const qualifier: {
    };

    @@public
    export const all: StaticDirectory = Transformer.sealDirectory(d`.`, [
        f`AriaTenantToken.cs`,
    ]);
}
