Examples on how to connect to Dynamics 365 CRM On-Prem using Dataverse Service Client (DVSC)

In all cases, populate appsettings.json with your values for eaceh project.

ADFS requires specific configuration before all of this works.

# ADFS2019ConnectionString

Connect to Dynamics 365 CRM On-Prem using ADFS 2019 and OAuth 2.0 using only connection string

- [Authorization code grant](https://learn.microsoft.com/en-us/azure/active-directory/develop/msal-authentication-flows#authorization-code): `AuthType=OAuth;Url=https://org.crm.example.com/;Username=user@example.com;Password=<pw>;RedirectUri=http://localhost:54321;AppId=174697c7-4ec8-401a-88ce-e6af4d05b6dd;LoginPrompt=Always`
   
   UI required (browser). Takes 2 requests to get access token. MSAL library takes care of listening to particular port specified in RedirectUri, so that when first request goes out to ADFS and you get authorization code, browser on your computer where the app runs can get the response, extract authorization code and issue another request to ADFS to finally get the access token.
- [Resource Owner Password Credentials grant](https://learn.microsoft.com/en-us/azure/active-directory/develop/v2-oauth-ropc): `AuthType=OAuth;Url=https://org.crm.example.com/;Username=user@example.com;Password=<pw>;AppId=174697c7-4ec8-401a-88ce-e6af4d05b6dd;LoginPrompt=Never`
   
   You authenticate as particular user. No UI required. Takes 1 request to get access token.
   
   This flow is considered for Desktop and Mobile application types, however I am going to use it for Service (Daemon) application as it requires no user interaction and I'm not so sure if [Client credentials](https://learn.microsoft.com/en-us/azure/active-directory/develop/msal-authentication-flows#client-credentials) flow (which is officially meant for daemon applications) can be supported by on-premises instance.
   
   Has drawbacks - no MFA support, passwords with leading/trailing whitespace not supported etc. See [notes](https://learn.microsoft.com/en-us/azure/active-directory/develop/v2-oauth-ropc) and consider what applies to on-premises.


# ADFS2019DeviceFlow

Connect to Dynamics 365 CRM On-Prem using tokenProvider to use MSAL library and implement device flow

# ADFS2016

MSAL is not compatible with ADFS2016, thus DVSC cannot be used. Instead, use tokenProvider to get access token and use it to connect to Dynamics 365 CRM On-Prem.


# Configuring CRM/ADFS 2019
1. You must have: Windows Server 2019+/ADFS 2019+
2. [KB4490481](https://support.microsoft.com/en-us/topic/march-26-2019-kb4490481-os-build-17763-402-c323e5c1-d524-dbdb-04a0-c3b5c8c8f2fd) must be installed. Required for [MSAL support](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/ADFS-support#case-where-msal-connects-directly-to-adfs).
3. [Configure the Microsoft Dynamics 365 Server for claims-based authentication](https://learn.microsoft.com/en-us/dynamics365/customerengagement/on-premises/deploy/configure-the-dynamics-365-server-for-claims-based-authentication?view=op-9-1) and [also for IFD](https://learn.microsoft.com/en-us/dynamics365/customerengagement/on-premises/deploy/configure-the-dynamics-365-server-for-ifd?view=op-9-1). Don't forget to do the appropriate configuration on AD FS server too for [claims]([Configure the AD FS server for claims-based authentication | Microsoft Learn](https://learn.microsoft.com/en-us/dynamics365/customerengagement/on-premises/deploy/configure-the-ad-fs-server-for-claims-based-authentication?view=op-9-1)) and [IFD auth](https://learn.microsoft.com/en-us/dynamics365/customerengagement/on-premises/deploy/configure-the-ad-fs-server-for-ifd?view=op-9-1).
   Regarding non-IFD, Claims-only configuration for internal access - I tested it with DVSC and as of writing, it didn't work. It did get the access token, but then failed to send requests to right URL - it didn't append the `/org` after the URL host part.  
   
   So I will assume the following:
   - ADFS is at https://adfs.example.com
   - CRM non-IFD URL for particular org is at https://crm.example.com/ORG
   - IFD URL for org is at: https://org.crm.example.com 
   
   You maybe have `org.example.com` IFD URL in real life if you had IFD configured previously as per MS documentation. See IFD configuration notes at the end of the article.
4. On ADFS Server you have to tell that you have to register your application. You will use ClientId in connection string. And you have to give permissions for this application to get access token for CRM IFD relying party.
   `RedirectUri` [must contain port](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/System-Browser-on-.Net-Core#limitations) and must use http protocol (for localhost). If port will not be specified, random will be used by MSAL when you connect via DVSC, but ADFS just won't accept it.
   ```powershell
   # MSAL will listen on localhost this particular port to get answer from ADFS (authorization code), but that is only true for authorization code grant flow: https://learn.microsoft.com/en-us/windows-server/identity/ad-fs/overview/ad-fs-openid-connect-oauth-flows-scenarios#authorization-code-grant-flow
   > Add-AdfsClient -ClientId ([guid]::NewGuid()) -Name "DVSC Client" -RedirectUri "http://localhost:54321" -Description "Dataverse Service Client will connect to CRM" -ClientType Public
   > $ClientId = (Get-AdfsClient -Name "DVSC Client").ClientId
   # If you don't grant permissions, you will get error: MSIS9605: The client is not allowed to access the requested resource. when trying to issue token at /adfs/oauth2/token.
   # For ServerRoleIdentifier, "external domain" URL must be used you chose when you configured IFD. You inputted 4 domains/urls, this would be the 4th one.
   # Another way to find out - go to ADFS, open Relying parties, open the IFD relying party (auth.crm.example.com), open Identities, and pick the first one: https://auth.crm.example.com/
   > Grant-AdfsApplicationPermission -ClientRoleIdentifier $ClientId -ServerRoleIdentifier "https://auth.crm.example.com/" -ScopeNames "openid"
   > $ClientId
    
   1260534b-00ca-4663-870f-d77b8d4ad6d3
   ```
   What does *openid* has to do anything with this? [It means]([AD FS OpenID Connect/OAuth concepts | Microsoft Learn](https://learn.microsoft.com/en-us/windows-server/identity/ad-fs/development/ad-fs-openid-connect-oauth-concepts#scopes)) that you allow your client application to use OpenID Connect (OIDC) authentication protocol.
5. [OAuth must be enabled for CRM](https://learn.microsoft.com/en-us/dynamics365/customerengagement/on-premises/deploy/post-installation-configuration-guidelines-dynamics-365?view=op-9-1#configure-windows-server-for-dynamics-365-customer-engagement-on-premises-applications-that-use-oauth). Execute this on CRM server:
   ```powershell
   Add-PSSnapin Microsoft.Crm.PowerShell  
   $ClaimsSettings = Get-CrmSetting -SettingType OAuthClaimsSettings  
   $ClaimsSettings.Enabled = $true  
   Set-CrmSetting -Setting $ClaimsSettings  
   ```
   If you want to validate OAuth is enabled, you must open: https://org.crm.example.com/XRMServices/2011/Organization.svc/web and see with, say, Fiddler, whether you get `WWW-Authenticate` header that is like: `Bearer redirect_uri=https://adfs.example.com/adfs/ls/`
   
   You may get multiple headers, like `WWW-Authenticate: Negotiate` and `WWW-Authenticate: NTLM`, but one with `Bearer` must be present. If you don't want to configure fiddler to catch TLS requests, you can check what headers that request returns like this:
   ```powershell
   > Invoke-WebRequest -Uri "https://org.crm.exmple.com/XRMServices/2011/Organization.svc/web" -UseBasicParsing
   > $error[0].exception.response.headers["WWW-Authenticate"]
   
   Bearer authorization_uri=https://adfs.example.com/adfs/oauth2/authorize, resource_id=https://org.crm.example.com/
   ```
   Don't worry, the Invoke-WebRequest returns 401 error - it is expected.
6.  As of writing, the DVSC will fail to connect if you get multiple WWW-Authenticate headers. It is due to a bug in the client itself and hopefully it gets fixed. [isOnPrem not passed down correctly when invoking GetAuthorityFromTargetServiceAsync · Issue #396 · microsoft/PowerPlatform-DataverseServiceClient (github.com)](https://github.com/microsoft/PowerPlatform-DataverseServiceClient/issues/396).
   
   Thus, at the time of writing, we need additional configuration:
   
   1. Open `C:\windows\system32\inetsrv\config\applicationHost.config` on CRM server.
   2. Find this XML element:
   ```xml
       <location path="Microsoft Dynamics CRM/XRMServices/2011/Organization.svc">
        <system.webServer>
            <security>
                <authentication>
                    <digestAuthentication enabled="false" />
                    <basicAuthentication enabled="false" />
                    <anonymousAuthentication enabled="true" />
                    <windowsAuthentication enabled="true" />
                </authentication>
            </security>
        </system.webServer>
    </location>
   ```
   3. Change windowsAuthentication to false:
   ```xml
       <location path="Microsoft Dynamics CRM/XRMServices/2011/Organization.svc">
        <system.webServer>
            <security>
                <authentication>
                    <digestAuthentication enabled="false" />
                    <basicAuthentication enabled="false" />
                    <anonymousAuthentication enabled="true" />
                    <windowsAuthentication enabled="false" />
                </authentication>
            </security>
        </system.webServer>
    </location>
   ```