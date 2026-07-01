using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Constants
{
    //add alll scenes here
    #region SCENES

    public const string SCENE_MENU           = "MenuScene";
    public const string SCENE_WEB            = "webscene";
    public const string SCENE_CREATURE        = "battleScene";
    public const string SCENE_BATTLE_PREP    = "BattlePrepScene";
    public const string SCENE_PROFILE_SETUP  = "ProfileSetupScene";
    public const string SCENE_STORE          = "StoreScene";
    public const string SCENE_PROFILE        = "PlayerProfileScene";

    #endregion


    //add string values here
    #region STRINGS

    public const string PRIVACY_URL = "http://sharpenminds.in:8081/consent/policy/GamePad";
    public const string PRIVACY_TITTLE = "Privacy Policy";
    public const string LICENCE_TITTLE = "Licence & Credits";
    public const string ATT_DESCRIPTION = "This identifier will be used to deliver personalized ads to you.";
    public const string RESTORE_PURCHASE_WARNING = "Restore Failed.. Seems like you have not previously purchased the plan.";
    public const string RESTORE_PURCHASE_SUCCESS = "Restore completed";
    public const string GAME_SHARE_TEXT = "Download UnityBasedProject game!\n ";
    public const string LICENSE_HTML = @"<html>
<head>
    <title>License Detail</title>
    <meta http-equiv=""content-type"" content=""text/html;charset=utf-8""/>
</head>
<body >

<div id=""wrap"">

    <div id=""content"">
        <div id=""left"">
 
<!-- Glide -->
		


		
            <!-- TextMesh Pro -->
            <h2><u>TextMesh Pro (Free)</u></h2>
            <p>https://assetstore.unity.com/packages/essentials/beta-projects/textmesh-pro-84126</p>

            <p>TextMesh Pro is the ultimate text solution for Unity. It's the perfect replacement for Unity's UI Text & Text Mesh.

Powerful and easy to use, TextMesh Pro uses Advanced Text Rendering techniques along with a set of custom shaders; delivering substantial visual quality improvements while giving users incredible flexibility when it comes to text styling and texturing.

TextMesh Pro provides Improved Control over text formatting and layout with features like character, word, line and paragraph spacing, kerning, justified text, Links, over 30 Rich Text Tags available, support for Multi Font & Sprites, Custom Styles and more.

Great performance. Since the geometry created by TextMesh Pro uses two triangles per character just like Unity's text components, this improved visual quality and flexibility comes at no additional performance cost.

Optimized for Desktop & Mobile devices, TextMesh Pro brings State-Of-The-Art text rendering to Unity.</p>
<!-- Glide -->
            <h2><u>Glide</u></h2>
            <p>Copyright 2014 Google Inc.</p>

            <p>THIS SOFTWARE IS PROVIDED BY GOOGLE, INC. ``AS IS'' AND ANY EXPRESS OR IMPLIED
                WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY
                AND
                FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL GOOGLE, INC. OR
                CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
                CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE
                GOODS OR
                SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED
                AND ON
                ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
                NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
                ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.</p>

            <p>Copyright 2012 Jake Wharton</p>
            <p> Copyright 2011 The Android Open Source Project</p>

            <p>Licensed under the Apache License, Version 2.0 (the ""License"");
                you may not use this file except in compliance with the License.
                You may obtain a copy of the License at</p>
            <p>http://www.apache.org/licenses/LICENSE-2.0</p>

            <p>Unless required by applicable law or agreed to in writing, software
                distributed under the License is distributed on an ""AS IS"" BASIS,
                WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
                See the License for the specific language governing permissions and
                limitations under the License.</p>

            
           <!-- Gson -->
            <h2><u>Gson</u></h2>

            <p>Copyright 2008-2014 Google Inc.</p>
            <p>Licensed under the Apache License, Version 2.0 (the ""License"");
                you may not use this file except in compliance with the License.
                You may obtain a copy of the License at</p>
            <p>http://www.apache.org/licenses/LICENSE-2.0</p>

            <p>Unless required by applicable law or agreed to in writing, software
                distributed under the License is distributed on an ""AS IS"" BASIS,
                WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
                See the License for the specific language governing permissions and
                limitations under the License.</p>
<!-- WebView -->
            <h2><u>unity-webview</u></h2>

            <p>Copyright (C) 2011 Keijiro Takahashi</p>
			<p>Copyright (C) 2012 GREE, Inc.</p>
            <p>        The zlib License</p>
           

            <p>This software is provided 'as-is', without any express or implied
warranty.  In no event will the authors be held liable for any damages
arising from the use of this software.</p>

<p>Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it
freely, subject to the following restrictions:</p>

<p>1. The origin of this software must not be misrepresented; you must not
   claim that you wrote the original software. If you use this software
   in a product, an acknowledgment in the product documentation would be
   appreciated but is not required.</p>
<p>2. Altered source versions must be plainly marked as such, and must not be
   misrepresented as being the original software.</p>
<p>3. This notice may not be removed or altered from any source distribution.
          </p>
 




        </div>
        <p></p>
    </div>

</div>
</body>
</html>";

    #endregion

    //add analytics events name here
    #region ANALYTICS

    public const string EVENT_GAME_LAUNCH ="game_launch";
    public const string EVENT_APP_OPEN = "app_open_event";
    public const string EVENT_STORE_SCENE_OPEN = "store_scene_open";
    public const string EVENT_PROFILE_SCENE_OPEN = "profile_scene_open";
    public const string EVENT_LOGOUT_CLICK = "logout_click";
    public const string EVENT_PROFILE_CREATE_OPEN = "profile_create_open";

    #endregion

    //add integer constant here

    //warning messages
    public const string WARN_SOMETHING_WRONG = "Something went wrong,Please try again later.";
    public const string WARN_NO_INTERNET = "Internet connection required,Please try again.";

}
