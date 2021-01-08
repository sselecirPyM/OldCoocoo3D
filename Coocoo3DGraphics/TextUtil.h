#pragma once
#include "pch.h"
inline char* CooGetMStr(const wchar_t* wstr1)
{
	UINT length1 = wcslen(wstr1);
	UINT strlen1 = WideCharToMultiByte(CP_ACP, 0, wstr1, length1, NULL, 0, NULL, NULL);
	char* str1 = (char*)malloc(strlen1 + 1);
	WideCharToMultiByte(CP_ACP, 0, wstr1, length1, str1, strlen1, NULL, NULL);
	str1[strlen1] = 0;
	return str1;
}

inline void CooFreeMStr(char* str1)
{
	free(str1);
}

inline int CooBomTest(void* str1)
{
	static const char* a = "\xef\xbb\xbf";
	if (memcmp(a, str1, 3) == 0)
	{
		return 3;
	}
	//if (str1[0] == 0xEF&& str1[1] == 0xBB && str1[2] == 0xBF)
	//{
	//	return 3;
	//}
	return 0;
}