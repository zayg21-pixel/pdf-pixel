// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { dotnet } from './_framework/dotnet.js'

export async function GetInterop() {
    const { getAssemblyExports } = await dotnet
        .withDiagnosticTracing(false)
        .create();

    const exports = await getAssemblyExports("PdfReader.Web.Demo.dll");
    return exports.PdfReader.Web.Demo.DocumentInterop;
}
