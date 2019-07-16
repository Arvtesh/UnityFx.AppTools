
NSString * _kaCreateNSString(const char * string)
{
	if (string)
	{
		return [NSString stringWithUTF8String: string];
	}

	return [NSString stringWithUTF8String: ""];
}

char * _kaMakeStringCopy(const char * string)
{
	if (string)
	{
		char * res = (char *)malloc(strlen(string) + 1);
		strcpy(res, string);
		return res;
	}

	return nil;
}

extern "C"
{
	const char * _GetKeychainValue(const char * key)
	{
		NSMutableDictionary * keychainItem = [[NSMutableDictionary alloc] init];
		NSString * strKey = _kaCreateNSString(key);
		CFDictionaryRef result = nil;
		OSStatus status;

		keychainItem[(__bridge id)kSecClass] = (__bridge id)kSecClassGenericPassword;
		keychainItem[(__bridge id)kSecAttrAccessible] = (__bridge id)kSecAttrAccessibleAlways;
		keychainItem[(__bridge id)kSecAttrAccount] = strKey;
		keychainItem[(__bridge id)kSecAttrService] = strKey;
		keychainItem[(__bridge id)kSecReturnData] = (__bridge id)kCFBooleanTrue;
		keychainItem[(__bridge id)kSecReturnAttributes] = (__bridge id)kCFBooleanTrue;

		status = SecItemCopyMatching((__bridge CFDictionaryRef)keychainItem, (CFTypeRef *)&result);

		if (status == noErr)
		{
			NSDictionary * resultDict = (__bridge_transfer NSDictionary *)result;
			NSData * data = resultDict[(__bridge id)kSecValueData];

			if (data)
			{
				unsigned long length = [data length];
				char * res = (char *)malloc(length + 1);
				[data getBytes:res];
				res[length] = '\0';
				return res;
			}
		}

		return nil;
	}

	void _SetKeychainValue(const char * key, const char * value)
	{
		NSMutableDictionary * keychainItem = [[NSMutableDictionary alloc] init];
		NSString * strKey = _kaCreateNSString(key);
		NSString * strValue = _kaCreateNSString(value);

		keychainItem[(__bridge id)kSecClass] = (__bridge id)kSecClassGenericPassword;
		keychainItem[(__bridge id)kSecAttrAccessible] = (__bridge id)kSecAttrAccessibleAlways;
		keychainItem[(__bridge id)kSecAttrAccount] = strKey;
		keychainItem[(__bridge id)kSecAttrService] = strKey;
		keychainItem[(__bridge id)kSecValueData] = [strValue dataUsingEncoding:NSUTF8StringEncoding];
		SecItemAdd((__bridge CFDictionaryRef)keychainItem, nil);
	}
}

