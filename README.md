# What is PgMessenger?
PgMessenger is a Windows program (download [here](https://github.com/dlebansais/PgMessenger/releases/download/v1.0.0.61/PgMessenger.zip)) that let you read the game chat of *Project: Gorgon* on any computer.

![Screenshot](/Doc/Screenshot.png?raw=true "Screenshot Example")


## How does it work?
The *Project: Gorgon* game client -when configured to do so- can save a log of in-game chat in files. Any player running PgMessenger will send new chat lines to a dedicated server, and anyone running PgMessenger will get updates from that server and display it in real time. Therefore, the system just need a handful of people playing across times zones and running PgMessenger for everyone to see chat 24/7.

All common channels are captured: Global, Help and Trade. Custom room channels are not. Guild channel is also captured but visible only to guild members through a few tricks (see the [guild chat](#guild-chat) section).

Eventually, PgMessenger will be changed to use a game chat API, but until then log files are the only way to do this.

# Setting up
To contribute and send updates to others, follow these instructions:
1. In the game, open the Settings window, select Special settings and add **LogChat** in the list.

![LogChat](/Doc/GameSettings.png?raw=true "The Settings window")

2. [Download](https://github.com/dlebansais/PgMessenger/releases/download/v1.0.0.61/PgMessenger.zip) and extract PgMessenger.zip somewhere on your computer.
3. Run PgMessenger.exe, this will add a little icon in the task bar, possibly in the taskbar icon window depending on your version of Windows and settings.
4. Right-click the icon to bring up a menu and choose **Load At Startup**.
5. The dialog box that pops up gives you the two options that can set PgMessenger to load at startup: either run the program as administrator (never recommended, unless you really trust me), or follow specific instructions. You could start the program manually every time too but it's quite annoying.
6. Right-click the icon again and select **Settings** to bring the PgMessenger settings window up. This is where you decide if you want to log guild chat, and if you do what are the guild affiliations of your characters (this is not in logs). The [guild chat](#guild-chat) section has more info about these settings.
6. Make sure you quit and restart the game client, PgMessenger needs to capture chat log when you log in.

If you just want to read in-game chat, [download](https://github.com/dlebansais/PgMessenger/releases/download/v1.0.0.61/PgMessenger.zip) and extract PgMessenger.zip, then run PgMessenger.exe.

# Guild chat
The guild chat is particularly challenging to get right because of obvious privacy issues. One significant challenge is to not make this chat readable to anyone outside the guild (notably me, the developer of this), while allowing guild mates to read it from any computer. I will describe here how it is done.

First of all, guild chat captured from a game log file is encrypted before being sent to the server. This ensures that nobody without access to the computer with these file can read it. The text is encrypted with a password, assumed to be shared among guild members, and can be decrypted by anyone knowing the password.

This password is, of course, not on the server but stored locally. This can be done in the PgMessenger Settings window:

![LogChat](/Doc/PgMessengerSettings.png?raw=true "The Settings window")

These settings work as follow:

* You can completely disable guild chat and this is the default.
* If you enable guild chat, you must indicate for each guild its name and the password used to encrypt/decrypt chat. If you are using a wrong or outdated password, only you can read what you send.
* You can tell PgMessenger about a character and its guild affiliation, this will be visible to other guild members, and they will know:
  * That you have sent guild chat to the server.
  * When you are connected.
  * Possibly when you are reading guild chat offline, although this particular feature is not implemented, but there is provision for it.
* The **Auto** checkbox is used in coordination with the guild Message of the Day. The MotD is *never* sent to the server (even encrypted), but if the MotD contains the pattern 'PgMessenger:' then the password is automatically extracted every time a player logs in or the MotD is changed.

Consider for example what happens if a player is removed from a guild. They can still see guild chat because they know the password. But if the password is changed in the MotD, all players sending guild chat lines will instantly use the new one (provided they are set on Auto). Offline members will also get the new password as soon as they log in.

There is a known vulnerability with this system: it's not particularly hard to fake the name of guild member (since that's quite public in the game) and read chat. As long as that person doesn't try to also read it, in which case the name is duplicated and people can start to ask questions, and as long as the password doesn't change, it will go unnoticed.

# F.A.Q.

1. What about an android or iOS app?

	This program is only for Windows users, tested on Windows 10 64-bits. Mobile apps aren't my area and there is a game API coming eventually, so not really worth the effort.

2. I'm trying to read chat but it's not updated.

	This is a community application, if nobody runs it then it doesn't work. It does takes at least a handful of players to kick off.

3. I have restarted the game but it still doesn't see me online.

	Depending on if you start the client from the game launcher or directly, a different log file is generated. This makes it rather complicated to find and read, and PgMessenger can miss the login message. To avoid this problem, always start the game the same way. Either directly, or from the game launcher, but don't mix. If you keep restarting it, eventually it should work.
    
4. Item links don't work.
	
    This feature is not available yet, but I'm thinking of adding it.
    
5. Isn't LogChat a VIP feature?
	
    It will be some day but not today. Hopefully, when it becomes VIP-only, the chat API is available and it's no longer a concern.

6. PgMessenger claims I'm connected but I'm not.
	
    Currently, if you just log out it doesn't say in logs, not until you log on a different character or quit the game.

7. How far back can I see chat?
	
    It's defined server-side, currently it's one hour before messages start to be erased.

8. You said guild chat is encrypted. What encryption are you using?

	I'm using AES then HMAC. All the gory details are [here](https://github.com/dlebansais/PgMessenger/blob/master/PgMessenger/Encryption.cs).
    
9. How do I know I can trust you with this app?

	It's [open source](https://github.com/dlebansais/PgMessenger). If you still don't trust it, don't run the fuckin app.
    
10. What about the recent Meltdown and Spectre vulnerabilities?

	The server has been fixed with a patch. Beside, does it really matter? If security is so important for you, see the answer above.


# Certification
This program is digitally signed with a [CAcert](https://www.cacert.org/) certificate.

