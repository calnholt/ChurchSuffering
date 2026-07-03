using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Utils;
using System.Collections.Generic;
using System;

namespace Crusaders30XX.ECS.Systems
{
  internal static class GuardianAngelMessageService
  {
    public static string GetMessage(GuardianMessageType guardianMessageType)
    {
        var messages = GetMessages(guardianMessageType);
        return messages[Random.Shared.Next(messages.Count)];
    }
    private static List<string> GetMessages(GuardianMessageType guardianMessageType)
    {
        switch (guardianMessageType)
        {
            case GuardianMessageType.StartOfBattle:
            {
                return [
                    "You don't scare us! :P",
                    "Here we go!",
                    "You've got this!",
                    "For Christ!",
                    "Tiny wings, big faith! ^_^",
                    "Team halo, roll out!"
                ];
            }
            case GuardianMessageType.Ambush:
            {
                return [
                    "Eek! Surprise? My halo's still sharp! >:3",
                    "Sneaky! But I'm sneakier~",
                    "You startled me... now repent!"
                ];
            }
            case GuardianMessageType.Intimidate:
            {
                return [
                    "Big and scary? I'm small and holy! ^_^",
                    "Your roar is loud, my faith is louder!",
                    "Kneel or be bonked by justice!",
                    "Cute doesn't mean weak, okay?"
                ];
            }
            case GuardianMessageType.ActionPhase:
            {
                return [
                    "We're not going to let you get away with that! ^^;",
                    "Action time! Wings up!",
                    "Smite routine: ready~",
                    "Blessings, engage!"
                ];
            }
            case GuardianMessageType.Attack:
            {
                return [
                    "Bonk of righteousness!",
                    "Smite, smite, smite! ^o^",
                    "Sword of light, go!",
                    "Take that! In Jesus' name!"
                ];
            }
            case GuardianMessageType.Burn:
            {
                return [
                    "Hot! Hot! Holy water, please!",
                    "We can take the heat—faith SPF 100!",
                    "Owies! Keep calm, keep praying!",
                    "Fire can't melt halos!"
                ];
            }
            case GuardianMessageType.Frozen:
            {
                return [
                    "B-b-brr... my wings are t-t-tiny...",
                    "Ice? More like nice try!",
                    "Huddle close! Holy cocoa time!",
                    "Freeze tag? I didn't say 'go'!"
                ];
            }
            case GuardianMessageType.BreakFrozen:
            {
                return [
                    "Thawed! Wiggle wiggle wings! ^_^",
                    "Back to warm and smitey!",
                    "Thanks! Movement restored!",
                    "No more ice. Let's dance!"
                ];
            }
            case GuardianMessageType.Penanace:
            {
                return [
                    "Mercy given, but lessons learned.",
                    "Confess and be bonked gently.",
                    "Repentance is cute, too!"
                ];
            }
            case GuardianMessageType.Win:
            {
                return [
                    "Victory! Praise be! ^_^",
                    "We did it! Group hug!",
                    "Halo high-five!",
                    "Another soul saved!"
                ];
            }
            case GuardianMessageType.GleeberIdle:
            {
                return [
                    "Demons aren't allowed to be this cute!",
                    "We're going to squeeze you to death!",
                ];
            }
            case GuardianMessageType.DemonIdle:
            {
                return [
                    "Stop glaring. It makes your horns droop.",
                    "Demon, do you need a hug? ...No? Thought so.",
                    "I'm watching you with big sparkly eyes. >_<",
                    "Back away from the villagers!"
                ];
            }
            case GuardianMessageType.OgreIdle:
            {
                return [
                    "Big stompy boots! Try tiptoeing?",
                    "Ogre-san, prepare for holy bonk!",
                    "Please don't sit on the church pews.",
                    "I'll polish your club—with your defeat!"
                ];
            }
            case GuardianMessageType.SuccubusIdle:
            {
                return [
                    "Modesty cape activated! >///<",
                    "Temptation rejected. Faith accepted.",
                    "Nice try, sinful lady!",
                    "My halo has ad blocker."
                ];
            }
            case GuardianMessageType.SpiderIdle:
            {
                return [
                    "Eight legs? I have infinite courage!",
                    "No webbing my hair, okay? It's fluffy!",
                    "Shoo, skitterbug!",
                    "Tiny but brave vs. spidey!"
                ];
            }
            case GuardianMessageType.Temperance:
            {
                return [
                    "Don't call it a comeback! ^_^",
                    "Self-control mode: ON.",
                    "Sip water, say prayer, steady heart.",
                    "We rise by kneeling."
                ];
            }
            default: return ["Whoops I forgot what I was gunna say :/"];
        }
    }
  }
}

public enum GuardianMessageType
{
    StartOfBattle,
    Ambush,
    Intimidate,
    ActionPhase,
    Attack,
    Burn,
    Frozen,
    BreakFrozen,
    Penanace,
    Win,
    DemonIdle,
    OgreIdle,
    SuccubusIdle,
    SpiderIdle,
    GleeberIdle,
    Temperance,
}
