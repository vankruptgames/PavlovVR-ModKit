// Fill out your copyright notice in the Description page of Project Settings.

using UnrealBuildTool;
using System.Collections.Generic;

public class PavlovServerTarget : TargetRules
{
	public PavlovServerTarget(TargetInfo Target) : base(Target)
	{
		Type = TargetType.Server;
        LinkType = TargetLinkType.Monolithic;
        bUseLoggingInShipping = true;

        ExtraModuleNames.Add("Pavlov");
	}
}
