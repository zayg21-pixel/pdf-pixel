// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { dotnet } from './_framework/dotnet.js'

export async function GetInterop() {
    const { getAssemblyExports } = await dotnet
        .withDiagnosticTracing(false)
        .create();

    const exports = await getAssemblyExports("PdfRender.Web.Demo.dll");
    return exports.PdfRender.Web.Demo.DocumentInterop;
}
